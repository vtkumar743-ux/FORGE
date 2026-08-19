using Gym.Api.Contracts;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers.Portal;

/// <summary>
/// Referrals and the notifications centre (Module 3 — Referrals, Support).
///
/// An invitation is not just a row in a rewards table: it creates a real lead on the desk's
/// pipeline board with the referrer attached, so the follow-up that converts it is the same
/// automated sequence every other lead gets. A referral scheme nobody follows up on is a
/// discount the gym pays for twice.
/// </summary>
[Route("api/portal")]
public class PortalEngagementController : PortalControllerBase
{
    private readonly GymDbContext _db;
    private readonly INotificationDispatcher _notifier;
    private readonly IClock _clock;
    private readonly ILogger<PortalEngagementController> _log;

    public PortalEngagementController(
        GymDbContext db, INotificationDispatcher notifier, IClock clock, ILogger<PortalEngagementController> log)
    {
        _db = db;
        _notifier = notifier;
        _clock = clock;
        _log = log;
    }

    // ================================================================== referrals

    [HttpGet("referrals")]
    [ProducesResponseType(typeof(PortalReferralResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PortalReferralResponse>> Referrals(CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == memberId, ct);
        if (member is null) return NoMemberProfile();

        var code = await EnsureCodeAsync(member, ct);

        var rows = await _db.Referrals
            .AsNoTracking()
            .Where(r => r.ReferrerMemberId == memberId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);

        var brand = await _db.SiteSettings.AsNoTracking()
            .Where(s => s.Key == "brand.name")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct) ?? "FORGE";

        var reward = rows.Count > 0 ? rows[0].RewardAmount : 500m;

        return Ok(new PortalReferralResponse
        {
            Code = code,
            ShareUrl = $"/free-trial?ref={code}",
            ShareMessage =
                $"I train at {brand}. Use my code {code} when you book a free trial and we both get " +
                $"₹{reward:N0} off. {member.HomeBranch?.Name ?? "Any branch"} — come try a class.",
            RewardAmount = reward,
            Invited = rows.Count,
            Joined = rows.Count(r => r.Status is ReferralStatus.Converted or ReferralStatus.Rewarded),
            Rewarded = rows.Count(r => r.ReferrerRewarded),
            CreditEarned = rows.Where(r => r.ReferrerRewarded).Sum(r => r.RewardAmount),
            CreditPending = rows
                .Where(r => r.Status == ReferralStatus.Converted && !r.ReferrerRewarded)
                .Sum(r => r.RewardAmount),
            Rows = rows.Select(r => new PortalReferralRow
            {
                Id = r.Id,
                InviteeName = r.InviteeName,
                // The member typed the number, but it is still someone else's — masked on the way back.
                InviteePhone = Mask(r.InviteePhone),
                Status = r.Status,
                StatusName = r.Status.ToString(),
                RewardAmount = r.RewardAmount,
                ReferrerRewarded = r.ReferrerRewarded,
                InvitedAtUtc = r.CreatedAtUtc,
                ConvertedAtUtc = r.ConvertedAtUtc,
                ExpiresOn = r.ExpiresOn?.ToString("yyyy-MM-dd")
            }).ToList()
        });
    }

    /// <summary>Invites one person by name and number, and puts them on the pipeline board.</summary>
    [HttpPost("referrals")]
    [ProducesResponseType(typeof(PortalReferralRow), StatusCodes.Status201Created)]
    public async Task<ActionResult<PortalReferralRow>> Invite(PortalInviteRequest request, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var phone = NormalisePhone(request.Phone);
        if (phone.Length != 10)
            return Problem("Enter a 10-digit mobile number.", statusCode: StatusCodes.Status400BadRequest);
        if (string.IsNullOrWhiteSpace(request.Name))
            return Problem("Who are you inviting?", statusCode: StatusCodes.Status400BadRequest);

        var member = await _db.Members.Include(m => m.HomeBranch).FirstOrDefaultAsync(m => m.Id == memberId, ct);
        if (member is null) return NoMemberProfile();

        if (phone == NormalisePhone(member.Phone))
            return Problem("That is your own number.", statusCode: StatusCodes.Status400BadRequest);

        if (await _db.Members.AnyAsync(m => m.Phone == phone, ct))
            return Problem("That number already belongs to a member here.", statusCode: StatusCodes.Status409Conflict);

        if (await _db.Referrals.AnyAsync(r => r.ReferrerMemberId == memberId && r.InviteePhone == phone, ct))
            return Problem("You have already invited that number.", statusCode: StatusCodes.Status409Conflict);

        var code = await EnsureCodeAsync(member, ct);
        var now = _clock.UtcNow;
        var branchId = request.BranchId ?? member.HomeBranchId;
        var name = request.Name.Trim();

        // Reuse an open lead on the same number rather than creating a duplicate person on the board.
        var lead = await _db.Leads
            .Where(l => l.Phone == phone && l.Stage != LeadStage.Joined && l.Stage != LeadStage.Lost)
            .OrderByDescending(l => l.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (lead is null)
        {
            lead = new Lead
            {
                FullName = name,
                Phone = phone,
                BranchId = branchId,
                Stage = LeadStage.Inquiry,
                Source = LeadSource.Referral,
                SourceDetail = $"Referred by {member.FullName} ({member.MemberCode})",
                Message = $"Referral code {code}.",
                SequenceActive = true,
                NextFollowUpAtUtc = now.AddMinutes(5),
                CreatedBy = member.MemberCode
            };
            _db.Leads.Add(lead);

            _db.LeadFollowUps.Add(new LeadFollowUp
            {
                Lead = lead,
                Channel = FollowUpChannel.WhatsApp,
                DueAtUtc = now.AddMinutes(5),
                Owner = "Automated",
                IsAutomated = true,
                StageAtCreation = LeadStage.Inquiry,
                Notes = $"Referred by {member.FullName}. Open with the referral, not a cold pitch."
            });
        }

        var referral = new Referral
        {
            ReferrerMemberId = memberId,
            Code = code,
            InviteeName = name,
            InviteePhone = phone,
            Lead = lead,
            Status = ReferralStatus.Sent,
            RewardAmount = 500m,
            // A referral that never expires is a liability the gym carries for ever.
            ExpiresOn = _clock.Today.AddDays(90),
            CreatedBy = member.MemberCode
        };
        _db.Referrals.Add(referral);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("{Member} referred {Name} — lead #{LeadId}", member.MemberCode, name, lead.Id);

        await _notifier.SendAsync(new OutboundMessage
        {
            MemberId = memberId,
            Kind = NotificationKind.General,
            Title = "Invitation sent",
            Body = $"We will reach out to {name} today. Your ₹{referral.RewardAmount:N0} credit lands when they join.",
            ActionUrl = "/portal/referrals",
            TemplateKey = "referral.sent"
        }, ct);

        return Created($"/api/portal/referrals/{referral.Id}", new PortalReferralRow
        {
            Id = referral.Id,
            InviteeName = referral.InviteeName,
            InviteePhone = Mask(referral.InviteePhone),
            Status = referral.Status,
            StatusName = referral.Status.ToString(),
            RewardAmount = referral.RewardAmount,
            ReferrerRewarded = false,
            InvitedAtUtc = referral.CreatedAtUtc,
            ConvertedAtUtc = null,
            ExpiresOn = referral.ExpiresOn?.ToString("yyyy-MM-dd")
        });
    }

    // ================================================================== notifications

    [HttpGet("notifications")]
    [ProducesResponseType(typeof(PortalNotificationsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PortalNotificationsResponse>> Notifications(
        [FromQuery] bool unreadOnly = false, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        // In-app only: the WhatsApp and SMS copies of the same message are delivery records,
        // not a second inbox, and showing all of them would triple every notification.
        var query = _db.Notifications
            .AsNoTracking()
            .Where(n => n.MemberId == memberId && n.Channel == NotificationChannel.InApp);

        var total = await query.CountAsync(ct);
        var unread = await query.CountAsync(n => !n.IsRead, ct);

        if (unreadOnly) query = query.Where(n => !n.IsRead);

        var rows = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 200))
            .Select(n => new { n.Id, n.Kind, n.Title, n.Body, n.ActionUrl, n.IsRead, n.CreatedAtUtc })
            .ToListAsync(ct);

        return Ok(new PortalNotificationsResponse
        {
            Rows = rows.Select(n => new PortalNotificationRow
            {
                Id = n.Id,
                Kind = n.Kind,
                KindName = Spaced(n.Kind.ToString()),
                Title = n.Title,
                Body = n.Body,
                ActionUrl = n.ActionUrl,
                IsRead = n.IsRead,
                CreatedAtUtc = n.CreatedAtUtc
            }).ToList(),
            Unread = unread,
            Total = total
        });
    }

    [HttpPost("notifications/{id:int}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.MemberId == memberId, ct);
        if (notification is null) return NotFound();

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return NoContent();
    }

    [HttpPost("notifications/read-all")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var unread = await _db.Notifications
            .Where(n => n.MemberId == memberId && n.Channel == NotificationChannel.InApp && !n.IsRead)
            .ToListAsync(ct);

        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = _clock.UtcNow;
        }

        if (unread.Count > 0) await _db.SaveChangesAsync(ct);
        return Ok(new { marked = unread.Count });
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// Members seeded before referrals existed, and anyone whose code collides, get one here.
    /// The code is stable once minted — it goes on WhatsApp messages we cannot recall.
    /// </summary>
    private async Task<string> EnsureCodeAsync(Member member, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(member.ReferralCode)) return member.ReferralCode!;

        var stem = new string(member.FullName.Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant();
        if (stem.Length < 3) stem = "FRG";

        var candidate = $"{stem}{1000 + member.Id}";
        var attempt = 0;
        while (await _db.Members.AnyAsync(m => m.ReferralCode == candidate && m.Id != member.Id, ct))
            candidate = $"{stem}{1000 + member.Id + ++attempt * 7}";

        member.ReferralCode = candidate;
        await _db.SaveChangesAsync(ct);
        return candidate;
    }

    private static string NormalisePhone(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length > 10 && digits.StartsWith("91")) digits = digits[^10..];
        if (digits.Length > 10 && digits.StartsWith('0')) digits = digits[^10..];
        return digits;
    }

    private static string? Mask(string? phone) =>
        string.IsNullOrWhiteSpace(phone) ? null : phone.Length < 4 ? "••••" : $"••••••{phone[^4..]}";
}
