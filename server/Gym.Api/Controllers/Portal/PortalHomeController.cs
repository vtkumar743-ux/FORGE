using Gym.Api.Contracts;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers.Portal;

/// <summary>
/// The portal home screen, the digital membership card, and the member's own profile.
///
/// Home is one round trip on purpose: a member opens the app on a phone outside the gym,
/// and eight parallel requests on a patchy connection is eight chances to show a half-drawn
/// screen. Everything the first paint needs arrives together or not at all.
/// </summary>
[Route("api/portal")]
public class PortalHomeController : PortalControllerBase
{
    /// <summary>Five weeks — enough calendar for a streak to look like a habit, on one phone screen.</summary>
    private const int CalendarDays = 35;

    private readonly GymDbContext _db;
    private readonly IClock _clock;

    public PortalHomeController(GymDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    [HttpGet("home")]
    [ProducesResponseType(typeof(PortalHomeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PortalHomeResponse>> Home(CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var today = _clock.Today;
        var nowUtc = _clock.UtcNow;

        var member = await _db.Members
            .AsNoTracking()
            .Include(m => m.HomeBranch)
            .FirstOrDefaultAsync(m => m.Id == memberId, ct);
        if (member is null) return NoMemberProfile();

        var membership = await PortalMembershipController.LoadCurrentAsync(_db, memberId, today, ct);
        var access = await PortalSessions.LoadAccessAsync(_db, memberId, today, ct);
        var streak = await LoadStreakAsync(_db, memberId, today, ct);

        // Today's classes plus the next one ahead, in one read of the booking table.
        var bookings = PortalSessions.OnePerSession(await _db.Bookings
            .AsNoTracking()
            .Where(b => b.MemberId == memberId)
            .Where(b => b.Status == BookingStatus.Booked || b.Status == BookingStatus.Waitlisted
                     || b.Status == BookingStatus.Attended)
            .Where(b => b.ClassSession.SessionDate >= today)
            .ToListAsync(ct));

        var sessionIds = bookings.Keys.ToList();
        var sessions = await PortalSessions.Query(_db)
            .Where(s => sessionIds.Contains(s.Id))
            .OrderBy(s => s.StartsAtUtc)
            .ToListAsync(ct);

        var mapped = sessions
            .Select(s => PortalSessions.Map(s, bookings.GetValueOrDefault(s.Id), access, nowUtc))
            .ToList();

        var todays = mapped.Where(s => s.Date == today.ToString("yyyy-MM-dd")).ToList();
        var next = mapped.FirstOrDefault(s => s.StartsAtUtc > nowUtc);

        var openInvoices = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.MemberId == memberId && i.AmountDue > 0)
            .Where(i => i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Refunded
                     && i.Status != InvoiceStatus.Draft)
            .OrderBy(i => i.DueOn)
            .Select(i => new
            {
                i.Id, i.InvoiceNumber, i.IssuedOn, i.DueOn, i.Status,
                i.GrandTotal, i.AmountPaid, i.AmountDue,
                Description = i.Lines.Select(l => l.Description).FirstOrDefault()
            })
            .ToListAsync(ct);

        var invoiceRows = openInvoices.Select(i => new PortalInvoiceRow
        {
            Id = i.Id,
            InvoiceNumber = i.InvoiceNumber,
            IssuedOn = i.IssuedOn.ToString("yyyy-MM-dd"),
            DueOn = i.DueOn.ToString("yyyy-MM-dd"),
            Status = i.Status,
            StatusName = Spaced(i.Status.ToString()),
            GrandTotal = i.GrandTotal,
            AmountPaid = i.AmountPaid,
            AmountDue = i.AmountDue,
            Description = i.Description
        }).ToList();

        var occupancy = await OccupancyAsync(member.HomeBranchId, ct);

        var notifications = await _db.Notifications
            .AsNoTracking()
            .Where(n => n.MemberId == memberId && n.Channel == NotificationChannel.InApp)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(5)
            .Select(n => new
            {
                n.Id, n.Kind, n.Title, n.Body, n.ActionUrl, n.IsRead, n.CreatedAtUtc
            })
            .ToListAsync(ct);

        var announcements = notifications.Select(n => new PortalNotificationRow
        {
            Id = n.Id,
            Kind = n.Kind,
            KindName = Spaced(n.Kind.ToString()),
            Title = n.Title,
            Body = n.Body,
            ActionUrl = n.ActionUrl,
            IsRead = n.IsRead,
            CreatedAtUtc = n.CreatedAtUtc
        }).ToList();

        var unread = await _db.Notifications.CountAsync(
            n => n.MemberId == memberId && n.Channel == NotificationChannel.InApp && !n.IsRead, ct);

        var prompts = await PortalBookingController.LoadRatingPromptsAsync(_db, memberId, nowUtc, ct);
        var celebration = await PortalTrainingController.LoadPendingCelebrationAsync(_db, memberId, ct);

        var newBadges = await _db.MemberBadges
            .AsNoTracking()
            .Include(mb => mb.Badge)
            .Where(mb => mb.MemberId == memberId && !mb.IsSeen)
            .OrderByDescending(mb => mb.AwardedAtUtc)
            .Take(4)
            .ToListAsync(ct);

        var program = await PortalTrainingController.LoadProgramSummaryAsync(_db, memberId, today, ct);

        // Referral credit is only real once the invitee actually joined and was rewarded.
        var referralCredit = await _db.Referrals
            .AsNoTracking()
            .Where(r => r.ReferrerMemberId == memberId && r.ReferrerRewarded)
            .SumAsync(r => (decimal?)r.RewardAmount, ct) ?? 0m;

        return Ok(new PortalHomeResponse
        {
            Member = Describe(member),
            Membership = membership,
            Streak = streak,
            HomeBranchOccupancy = occupancy,
            TodaysClasses = todays,
            NextClass = next,
            DuesOutstanding = invoiceRows.Sum(i => i.AmountDue),
            NextPayment = invoiceRows.FirstOrDefault(),
            UnreadNotifications = unread,
            Announcements = announcements,
            RatingPrompts = prompts,
            PendingCelebration = celebration,
            NewBadges = newBadges.Select(mb => new PortalBadgeRow
            {
                Id = mb.BadgeId,
                Name = mb.Badge.Name,
                Slug = mb.Badge.Slug,
                Description = mb.Badge.Description,
                IconKey = mb.Badge.IconKey,
                Tier = mb.Badge.Tier,
                AwardedAtUtc = mb.AwardedAtUtc,
                IsSeen = mb.IsSeen
            }).ToList(),
            Program = program,
            ReferralCredits = (int)referralCredit
        });
    }

    /// <summary>
    /// The digital membership card. The QR encodes the member's stable token, which is exactly
    /// what the desk kiosk matches on — so this screen and the scanner cannot drift apart.
    /// </summary>
    [HttpGet("card")]
    [ProducesResponseType(typeof(PortalCardResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PortalCardResponse>> Card(CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var today = _clock.Today;
        var member = await _db.Members
            .AsNoTracking()
            .Include(m => m.HomeBranch)
            .FirstOrDefaultAsync(m => m.Id == memberId, ct);
        if (member is null) return NoMemberProfile();

        var access = await PortalSessions.LoadAccessAsync(_db, memberId, today, ct);
        var subscription = access.Subscription;

        return Ok(new PortalCardResponse
        {
            MemberCode = member.MemberCode,
            FullName = member.FullName,
            PhotoUrl = member.PhotoUrl,
            QrToken = member.QrToken,
            HomeBranchName = member.HomeBranch.Name,
            PlanName = subscription?.Plan.Name,
            ValidUntil = subscription?.EndsOn.ToString("yyyy-MM-dd"),
            DaysLeft = subscription is null ? null : Math.Max(0, subscription.EndsOn.DayNumber - today.DayNumber),
            StatusName = member.Status.ToString(),
            IsUsable = access.BlockedReason is null,
            BlockReason = access.BlockedReason
        });
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(PortalMemberResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PortalMemberResponse>> Me(CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var member = await _db.Members.AsNoTracking().Include(m => m.HomeBranch)
            .FirstOrDefaultAsync(m => m.Id == memberId, ct);
        return member is null ? NoMemberProfile() : Ok(Describe(member));
    }

    /// <summary>
    /// The member edits their own goal, home branch, height and consents. Everything clinical —
    /// medical and injury notes — is accepted too, because the person with the injury is the
    /// one who knows about it before the coach does.
    /// </summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(PortalMemberResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PortalMemberResponse>> UpdateMe(
        UpdatePortalProfileRequest request, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var member = await _db.Members.Include(m => m.HomeBranch).FirstOrDefaultAsync(m => m.Id == memberId, ct);
        if (member is null) return NoMemberProfile();

        if (request.HomeBranchId is { } branchId && branchId != member.HomeBranchId)
        {
            var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == branchId && b.IsActive, ct);
            if (branch is null) return Problem("No such branch.", statusCode: StatusCodes.Status400BadRequest);

            // The member code carries the original branch's prefix and is printed on the card;
            // re-minting it would break every historical reference to this person.
            member.HomeBranchId = branchId;
            member.HomeBranch = branch;

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == member.UserId, ct);
            if (user is not null) user.HomeBranchId = branchId;
        }

        if (request.PrimaryGoal is not null) member.PrimaryGoal = Trim(request.PrimaryGoal, 200);
        if (request.HeightCm is { } height && height is > 90 and < 250) member.HeightCm = height;
        if (request.EmergencyContactName is not null) member.EmergencyContactName = Trim(request.EmergencyContactName, 120);
        if (request.EmergencyContactPhone is not null) member.EmergencyContactPhone = Trim(request.EmergencyContactPhone, 20);
        if (request.MedicalNotes is not null) member.MedicalNotes = Trim(request.MedicalNotes, 2000);
        if (request.InjuryNotes is not null) member.InjuryNotes = Trim(request.InjuryNotes, 2000);
        if (request.ConsentMarketing is { } marketing) member.ConsentMarketing = marketing;
        if (request.ConsentLeaderboard is { } leaderboard) member.ConsentLeaderboard = leaderboard;

        member.UpdatedBy = member.MemberCode;
        await _db.SaveChangesAsync(ct);

        return Ok(Describe(member));
    }

    // ---------------------------------------------------------------- helpers

    private static string? Trim(string value, int max)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0) return null;
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static PortalMemberResponse Describe(Core.Entities.Member m) => new()
    {
        Id = m.Id,
        MemberCode = m.MemberCode,
        FullName = m.FullName,
        FirstName = m.FullName.Split(' ')[0],
        PhotoUrl = m.PhotoUrl,
        Email = m.Email,
        Phone = m.Phone,
        HomeBranchId = m.HomeBranchId,
        HomeBranchName = m.HomeBranch.Name,
        HomeBranchSlug = m.HomeBranch.Slug,
        JoinedOn = m.JoinedOn.ToString("yyyy-MM-dd"),
        PrimaryGoal = m.PrimaryGoal,
        Status = m.Status,
        StatusName = m.Status.ToString(),
        HeightCm = m.HeightCm,
        StartWeightKg = m.StartWeightKg,
        DateOfBirth = m.DateOfBirth?.ToString("yyyy-MM-dd"),
        ConsentMarketing = m.ConsentMarketing,
        ConsentLeaderboard = m.ConsentLeaderboard,
        WaiverSigned = m.WaiverSigned
    };

    /// <summary>
    /// The streak flame and the calendar strip under it. Streak counters live on the member row
    /// (the kiosk advances them on check-in); the calendar is derived, so a corrected visit
    /// shows up here without anyone having to recompute a counter.
    /// </summary>
    internal static async Task<PortalStreakResponse> LoadStreakAsync(
        GymDbContext db, int memberId, DateOnly today, CancellationToken ct)
    {
        var member = await db.Members.AsNoTracking()
            .Select(m => new { m.Id, m.CurrentStreakDays, m.LongestStreakDays, m.LastVisitOn })
            .FirstAsync(m => m.Id == memberId, ct);

        var from = today.AddDays(-(CalendarDays - 1));
        var visits = await db.CheckIns
            .AsNoTracking()
            .Where(c => c.MemberId == memberId && !c.WasBlocked && c.VisitDate >= from)
            .Select(c => c.VisitDate)
            .ToListAsync(ct);
        var visited = visits.ToHashSet();

        var classes = await db.Bookings
            .AsNoTracking()
            .Where(b => b.MemberId == memberId && b.Status == BookingStatus.Attended)
            .Where(b => b.ClassSession.SessionDate >= from)
            .GroupBy(b => b.ClassSession.SessionDate)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count, ct);

        var calendar = new List<PortalCalendarDay>(CalendarDays);
        for (var date = from; date <= today; date = date.AddDays(1))
            calendar.Add(new PortalCalendarDay
            {
                Date = date.ToString("yyyy-MM-dd"),
                Visited = visited.Contains(date),
                ClassCount = classes.GetValueOrDefault(date),
                IsToday = date == today
            });

        // Weeks start Monday: an Indian gym's week does, and "this week" should match the wall
        // planner behind the desk rather than a Sunday-first locale default.
        var weekStart = today.AddDays(-((int)today.DayOfWeek + 6) % 7);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        return new PortalStreakResponse
        {
            CurrentStreakDays = member.CurrentStreakDays,
            LongestStreakDays = member.LongestStreakDays,
            LastVisitOn = member.LastVisitOn?.ToString("yyyy-MM-dd"),
            VisitsThisWeek = visits.Count(v => v >= weekStart),
            VisitsThisMonth = visits.Count(v => v >= monthStart),
            Calendar = calendar
        };
    }

    /// <summary>Live head-count for one branch — the same derivation the public gauge uses.</summary>
    private async Task<BranchOccupancyResponse?> OccupancyAsync(int branchId, CancellationToken ct)
    {
        var branch = await _db.Branches.AsNoTracking()
            .Where(b => b.Id == branchId)
            .Select(b => new { b.Id, b.Name, b.Slug, b.OccupancyCapacity })
            .FirstOrDefaultAsync(ct);
        if (branch is null) return null;

        var current = await _db.CheckIns.CountAsync(
            c => c.BranchId == branchId && c.CheckOutAtUtc == null && !c.WasBlocked, ct);

        var ratio = branch.OccupancyCapacity == 0 ? 0d : (double)current / branch.OccupancyCapacity;
        return new BranchOccupancyResponse
        {
            BranchId = branch.Id,
            BranchName = branch.Name,
            BranchSlug = branch.Slug,
            CurrentCount = current,
            Capacity = branch.OccupancyCapacity,
            PercentFull = (int)Math.Round(Math.Min(1d, ratio) * 100),
            Band = ratio switch { < 0.45 => OccupancyBand.Comfortable, < 0.75 => OccupancyBand.Busy, _ => OccupancyBand.Peak },
            AsOfUtc = _clock.UtcNow
        };
    }
}
