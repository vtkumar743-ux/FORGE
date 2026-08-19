using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.Services;

/// <summary>
/// Churn-risk radar (Module 4.3). Rules, not a model: the desk has to be able to read the
/// score back to the member ("you have not been in for three weeks and your renewal is on
/// Friday"), and a black box nobody can explain is a number nobody acts on.
///
/// The four signals the spec names each carry weight — visit-frequency drop, a failed or
/// unpaid payment, low class ratings, and no forward bookings — plus the two the data made
/// obvious once it existed: a term about to expire, and a broken streak.
/// </summary>
public class ChurnService
{
    private readonly GymDbContext _db;
    private readonly INotificationDispatcher _notifier;
    private readonly IClock _clock;
    private readonly ILogger<ChurnService> _log;

    public ChurnService(GymDbContext db, INotificationDispatcher notifier, IClock clock, ILogger<ChurnService> log)
    {
        _db = db;
        _notifier = notifier;
        _clock = clock;
        _log = log;
    }

    /// <summary>Re-scores every member the gym could still keep. Idempotent; safe to run hourly.</summary>
    public async Task<int> RescoreAllAsync(CancellationToken ct = default)
    {
        var today = _clock.Today;
        var now = _clock.UtcNow;

        var members = await _db.Members
            .Where(m => m.Status != MemberStatus.Cancelled)
            .ToListAsync(ct);
        if (members.Count == 0) return 0;

        var ids = members.Select(m => m.Id).ToList();

        // One grouped query per signal rather than per member: 200 members is 6 queries, not 1,200.
        var duesByMember = await _db.Invoices.AsNoTracking()
            .Where(i => ids.Contains(i.MemberId) && i.AmountDue > 0
                        && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid || i.Status == InvoiceStatus.Overdue))
            .GroupBy(i => i.MemberId)
            .Select(g => new { MemberId = g.Key, Due = g.Sum(x => x.AmountDue), Overdue = g.Count(x => x.DueOn < today) })
            .ToDictionaryAsync(x => x.MemberId, x => x, ct);

        var failedPayments = await _db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Failed && p.Invoice != null && ids.Contains(p.Invoice.MemberId)
                        && p.CreatedAtUtc >= now.AddDays(-45))
            .GroupBy(p => p.Invoice!.MemberId)
            .Select(g => new { MemberId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MemberId, x => x.Count, ct);

        var upcomingBookings = await _db.Bookings.AsNoTracking()
            .Where(b => ids.Contains(b.MemberId)
                        && (b.Status == BookingStatus.Booked || b.Status == BookingStatus.Waitlisted)
                        && b.ClassSession.StartsAtUtc >= now)
            .GroupBy(b => b.MemberId)
            .Select(g => new { MemberId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MemberId, x => x.Count, ct);

        var lowRatings = await _db.TrainerRatings.AsNoTracking()
            .Where(r => ids.Contains(r.MemberId) && r.Score <= 2 && r.CreatedAtUtc >= now.AddDays(-60))
            .GroupBy(r => r.MemberId)
            .Select(g => new { MemberId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MemberId, x => x.Count, ct);

        var endingSoon = await _db.Subscriptions.AsNoTracking()
            .Where(s => ids.Contains(s.MemberId) && s.Status == SubscriptionStatus.Active)
            .GroupBy(s => s.MemberId)
            .Select(g => new { MemberId = g.Key, EndsOn = g.Max(x => x.EndsOn) })
            .ToDictionaryAsync(x => x.MemberId, x => x.EndsOn, ct);

        // Visit-frequency *drop* needs two windows, not one: someone who trained four times a
        // week and now trains once is leaving, even though "once a week" alone looks fine.
        var priorCutoff = now.AddDays(-28);
        var windowStart = now.AddDays(-56);
        var visitWindows = await _db.CheckIns.AsNoTracking()
            .Where(c => ids.Contains(c.MemberId) && !c.WasBlocked && c.CheckInAtUtc >= windowStart)
            .GroupBy(c => c.MemberId)
            .Select(g => new
            {
                MemberId = g.Key,
                Recent = g.Count(x => x.CheckInAtUtc >= priorCutoff),
                Prior = g.Count(x => x.CheckInAtUtc < priorCutoff)
            })
            .ToDictionaryAsync(x => x.MemberId, x => x, ct);

        foreach (var member in members)
        {
            var reasons = new List<string>();
            var score = 0;

            var daysSinceVisit = member.LastVisitOn is null
                ? 999
                : today.DayNumber - member.LastVisitOn.Value.DayNumber;

            score += daysSinceVisit switch
            {
                <= 3 => 0, <= 7 => 8, <= 14 => 25, <= 21 => 42, <= 45 => 60, _ => 75
            };
            if (daysSinceVisit >= 10)
                reasons.Add(daysSinceVisit >= 900 ? "never visited" : $"no visit in {daysSinceVisit} days");

            if (visitWindows.TryGetValue(member.Id, out var window) && window.Prior >= 6)
            {
                var drop = 1d - (double)window.Recent / window.Prior;
                if (drop >= 0.5)
                {
                    score += 18;
                    reasons.Add($"visits down {(int)Math.Round(drop * 100)}% this month");
                }
                else if (drop >= 0.3)
                {
                    score += 9;
                    reasons.Add($"visits down {(int)Math.Round(drop * 100)}%");
                }
            }

            if (duesByMember.TryGetValue(member.Id, out var dues))
            {
                score += dues.Overdue > 0 ? 15 : 7;
                reasons.Add(dues.Overdue > 0 ? $"Rs {dues.Due:N0} overdue" : $"Rs {dues.Due:N0} outstanding");
            }

            if (failedPayments.TryGetValue(member.Id, out var failed) && failed > 0)
            {
                score += 12;
                reasons.Add(failed == 1 ? "a payment failed" : $"{failed} payments failed");
            }

            if (lowRatings.TryGetValue(member.Id, out var poor) && poor > 0)
            {
                score += 10;
                reasons.Add(poor == 1 ? "rated a class 2 stars or lower" : $"{poor} low class ratings");
            }

            if (!upcomingBookings.ContainsKey(member.Id) && member.Status == MemberStatus.Active)
            {
                score += 8;
                reasons.Add("nothing booked ahead");
            }

            if (endingSoon.TryGetValue(member.Id, out var endsOn))
            {
                var daysLeft = endsOn.DayNumber - today.DayNumber;
                if (daysLeft is >= 0 and <= 14)
                {
                    score += daysLeft <= 7 ? 12 : 6;
                    reasons.Add(daysLeft <= 0 ? "membership ends today" : $"renewal in {daysLeft} days");
                }
            }

            if (member.CurrentStreakDays == 0 && daysSinceVisit > 3) score += 6;
            if (member.Status == MemberStatus.Frozen) { score += 12; reasons.Add("membership frozen"); }
            if (member.Status == MemberStatus.Expired) { score += 15; reasons.Add("membership expired"); }

            member.ChurnScore = Math.Clamp(score, 0, 100);
            member.ChurnRisk = member.ChurnScore switch
            {
                < 20 => ChurnRiskBand.Healthy,
                < 40 => ChurnRiskBand.Watch,
                < 65 => ChurnRiskBand.Amber,
                _ => ChurnRiskBand.Red
            };
            member.ChurnReasons = reasons.Count == 0 ? null : string.Join(" · ", reasons.Take(4));
            member.ChurnScoredAtUtc = now;
        }

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Churn radar rescored {Count} members", members.Count);
        return members.Count;
    }

    /// <summary>
    /// One-click win-back. Mints a member-specific coupon through the existing coupon engine
    /// (so every cap and expiry the desk already understands still applies), messages the
    /// member on every channel the gym has enabled, and puts a call on the desk's own board.
    /// </summary>
    public async Task<WinBackResult> RunWinBackAsync(
        int memberId, WinBackOptions options, string actor, CancellationToken ct = default)
    {
        var member = await _db.Members.Include(m => m.HomeBranch)
            .FirstOrDefaultAsync(m => m.Id == memberId, ct);
        if (member is null) return new WinBackResult(false, "Member not found.", null, 0);

        var now = _clock.UtcNow;
        if (!options.Force && member.LastWinBackAtUtc is { } last && (now - last).TotalDays < 14)
            return new WinBackResult(false,
                $"A win-back already went out on {last.AddHours(5.5):dd MMM}. Chasing again this soon reads as spam — override if you have a reason.",
                null, 0);

        Coupon? coupon = null;
        if (options.DiscountPercent > 0)
        {
            var digits = new string(member.MemberCode.Where(char.IsLetterOrDigit).ToArray());
            var code = "BACK" + (digits.Length <= 6 ? digits : digits[^6..]).ToUpperInvariant();
            coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code == code, ct);
            var validTo = _clock.Today.AddDays(options.OfferValidDays);

            if (coupon is null)
            {
                coupon = new Coupon
                {
                    Code = code,
                    Name = $"Win-back · {member.FullName}",
                    Description = $"Personal win-back offer issued by {actor}.",
                    DiscountType = DiscountType.Percentage,
                    DiscountValue = options.DiscountPercent,
                    MaxDiscountAmount = options.MaxDiscountAmount,
                    ValidFrom = _clock.Today,
                    ValidTo = validTo,
                    UsageCap = 1,
                    PerMemberCap = 1,
                    IsActive = true,
                    ShowAsWebsiteBanner = false
                };
                _db.Coupons.Add(coupon);
            }
            else
            {
                // Re-issuing extends the same code rather than littering the catalogue.
                coupon.DiscountValue = options.DiscountPercent;
                coupon.ValidFrom = _clock.Today;
                coupon.ValidTo = validTo;
                coupon.UsageCap = coupon.UsageCount + 1;
                coupon.IsActive = true;
            }
        }

        var offerLine = coupon is null
            ? "Your spot is still here whenever you are."
            : $"Use code {coupon.Code} for {options.DiscountPercent:0.#}% off your renewal — valid until {coupon.ValidTo:dd MMM}.";

        var firstName = member.FullName.Split(' ')[0];
        var body = string.IsNullOrWhiteSpace(options.Message)
            ? $"Hi {firstName}, we have missed you at {member.HomeBranch.Name}. {offerLine} Reply here or call us and we will get your next session on the calendar."
            : options.Message.Trim();

        var channels = new List<NotificationChannel> { NotificationChannel.InApp };
        if (options.SendWhatsApp) channels.Add(NotificationChannel.WhatsApp);
        if (options.SendEmail && !string.IsNullOrWhiteSpace(member.Email)) channels.Add(NotificationChannel.Email);

        var written = await _notifier.SendAsync(new OutboundMessage
        {
            MemberId = member.Id,
            Kind = NotificationKind.WinBack,
            Title = "We have missed you",
            Body = body,
            ActionUrl = "/portal/membership",
            TemplateKey = "winback.offer",
            Channels = channels,
            PayloadJson = coupon is null ? null : $"{{\"coupon\":\"{coupon.Code}\"}}"
        }, ct);

        // The desk half of the sequence: a human call, because a message nobody follows up
        // on is a discount the gym pays for and gets nothing back from.
        written += await _notifier.SendAsync(new OutboundMessage
        {
            Kind = NotificationKind.General,
            Title = $"Win-back call due · {member.FullName}",
            Body = $"{member.MemberCode} · {member.Phone} · {member.ChurnReasons ?? "at risk"}. "
                   + (coupon is null ? "No offer attached." : $"Offer {coupon.Code} expires {coupon.ValidTo:dd MMM}."),
            ActionUrl = $"/admin/members/{member.Id}",
            TemplateKey = "winback.desk-task",
            Channels = new[] { NotificationChannel.InApp }
        }, ct);

        member.LastWinBackAtUtc = now;
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Win-back sent to member {MemberId} by {Actor} ({Coupon})",
            member.Id, actor, coupon?.Code ?? "no offer");

        return new WinBackResult(true,
            coupon is null
                ? $"Win-back sent to {member.FullName}."
                : $"Win-back sent to {member.FullName} with {options.DiscountPercent:0.#}% off (code {coupon.Code}).",
            coupon?.Code, written);
    }
}

public record WinBackOptions
{
    public decimal DiscountPercent { get; init; } = 20m;
    public decimal? MaxDiscountAmount { get; init; }
    public int OfferValidDays { get; init; } = 14;
    public string? Message { get; init; }
    public bool SendWhatsApp { get; init; } = true;
    public bool SendEmail { get; init; } = true;
    /// <summary>Overrides the fortnight cool-off; the desk has to mean it.</summary>
    public bool Force { get; init; }
}

public record WinBackResult(bool Sent, string Message, string? CouponCode, int ChannelRowsWritten);
