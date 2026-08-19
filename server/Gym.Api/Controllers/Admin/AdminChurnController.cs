using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers.Admin;

/// <summary>
/// The churn-risk radar and its win-back (Module 4.3). Every row carries the reasons it was
/// flagged, because the desk is about to pick up the phone and needs to know what to open
/// with — a score on its own tells them to call and nothing about what to say.
/// </summary>
[ApiController]
[Route("api/admin/churn")]
[Authorize(Roles = RoleNames.Admin)]
[Produces("application/json")]
public class AdminChurnController : ControllerBase
{
    private readonly GymDbContext _db;
    private readonly ChurnService _churn;
    private readonly IClock _clock;

    public AdminChurnController(GymDbContext db, ChurnService churn, IClock clock)
    {
        _db = db;
        _churn = churn;
        _clock = clock;
    }

    /// <summary>The radar itself: at-risk members, worst first, with the money at stake.</summary>
    [HttpGet("radar")]
    [ProducesResponseType(typeof(ChurnRadarResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChurnRadarResponse>> Radar(
        [FromQuery] int? branchId,
        [FromQuery] ChurnRiskBand? band,
        [FromQuery] int take = 60,
        CancellationToken ct = default)
    {
        var today = _clock.Today;

        var query = _db.Members.AsNoTracking()
            .Where(m => m.Status != MemberStatus.Cancelled);

        if (branchId is { } b) query = query.Where(m => m.HomeBranchId == b);
        query = band is { } wanted
            ? query.Where(m => m.ChurnRisk == wanted)
            : query.Where(m => m.ChurnRisk == ChurnRiskBand.Amber || m.ChurnRisk == ChurnRiskBand.Red);

        var rows = await query
            .OrderByDescending(m => m.ChurnScore)
            .ThenBy(m => m.LastVisitOn)
            .Take(Math.Clamp(take, 1, 200))
            .Select(m => new
            {
                m.Id, m.MemberCode, m.FullName, m.Phone, m.Email, m.PhotoUrl,
                BranchName = m.HomeBranch.Name, m.HomeBranchId,
                m.ChurnRisk, m.ChurnScore, m.ChurnReasons, m.LastVisitOn, m.CurrentStreakDays,
                m.Status, m.LastWinBackAtUtc,
                Subscription = m.Subscriptions
                    .Where(s => s.Status == SubscriptionStatus.Active)
                    .OrderByDescending(s => s.EndsOn)
                    .Select(s => new { s.EndsOn, PlanName = s.Plan.Name, s.PriceCharged })
                    .FirstOrDefault(),
                Dues = m.Invoices
                    .Where(i => i.AmountDue > 0 && i.Status != InvoiceStatus.Cancelled)
                    .Sum(i => i.AmountDue)
            })
            .ToListAsync(ct);

        var counts = await _db.Members.AsNoTracking()
            .Where(m => m.Status != MemberStatus.Cancelled)
            .Where(m => branchId == null || m.HomeBranchId == branchId)
            .GroupBy(m => m.ChurnRisk)
            .Select(g => new { Band = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var lastScored = await _db.Members.AsNoTracking().MaxAsync(m => (DateTime?)m.ChurnScoredAtUtc, ct);

        return Ok(new ChurnRadarResponse
        {
            ScoredAtUtc = lastScored,
            Healthy = counts.FirstOrDefault(c => c.Band == ChurnRiskBand.Healthy)?.Count ?? 0,
            Watch = counts.FirstOrDefault(c => c.Band == ChurnRiskBand.Watch)?.Count ?? 0,
            Amber = counts.FirstOrDefault(c => c.Band == ChurnRiskBand.Amber)?.Count ?? 0,
            Red = counts.FirstOrDefault(c => c.Band == ChurnRiskBand.Red)?.Count ?? 0,
            // What walks out of the door if nobody calls: the revenue attached to the flagged rows.
            RevenueAtRisk = rows.Sum(r => r.Subscription?.PriceCharged ?? 0m),
            Rows = rows.Select(r => new ChurnRadarRow
            {
                MemberId = r.Id,
                MemberCode = r.MemberCode,
                FullName = r.FullName,
                Phone = r.Phone,
                Email = r.Email,
                PhotoUrl = r.PhotoUrl,
                BranchId = r.HomeBranchId,
                BranchName = r.BranchName,
                Band = r.ChurnRisk,
                Score = r.ChurnScore,
                Reasons = string.IsNullOrWhiteSpace(r.ChurnReasons)
                    ? Array.Empty<string>()
                    : r.ChurnReasons.Split(" · ", StringSplitOptions.RemoveEmptyEntries),
                LastVisitOn = r.LastVisitOn?.ToString("yyyy-MM-dd"),
                DaysSinceVisit = r.LastVisitOn is null ? null : today.DayNumber - r.LastVisitOn.Value.DayNumber,
                CurrentStreakDays = r.CurrentStreakDays,
                Status = r.Status,
                PlanName = r.Subscription?.PlanName,
                PlanEndsOn = r.Subscription?.EndsOn.ToString("yyyy-MM-dd"),
                PlanValue = r.Subscription?.PriceCharged ?? 0m,
                AmountDue = r.Dues,
                LastWinBackAtUtc = r.LastWinBackAtUtc
            }).ToList()
        });
    }

    /// <summary>Re-scores on demand — the sweep runs every two hours, but the owner may not want to wait.</summary>
    [HttpPost("rescore")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Rescore(CancellationToken ct)
    {
        var scored = await _churn.RescoreAllAsync(ct);
        return Ok(new { scored, scoredAtUtc = _clock.UtcNow });
    }

    /// <summary>One-click win-back: personal offer, messages on every enabled channel, desk call queued.</summary>
    [HttpPost("winback/{memberId:int}")]
    [ProducesResponseType(typeof(WinBackResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WinBackResult>> WinBack(
        int memberId, WinBackRequest request, CancellationToken ct)
    {
        var result = await _churn.RunWinBackAsync(memberId, new WinBackOptions
        {
            DiscountPercent = Math.Clamp(request.DiscountPercent, 0m, 60m),
            MaxDiscountAmount = request.MaxDiscountAmount,
            OfferValidDays = Math.Clamp(request.OfferValidDays, 3, 90),
            Message = request.Message,
            SendWhatsApp = request.SendWhatsApp,
            SendEmail = request.SendEmail,
            Force = request.Force
        }, User.Identity?.Name ?? "admin", ct);

        // A refusal here is a cool-off, not a failure — 409 so the UI can offer the override.
        return result.Sent ? Ok(result) : Conflict(result);
    }

    /// <summary>
    /// The same sequence across a selection. Capped, and each refusal is reported rather than
    /// swallowed: the desk needs to know which of the twenty they picked actually went out.
    /// </summary>
    [HttpPost("winback/bulk")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> WinBackBulk(BulkWinBackRequest request, CancellationToken ct)
    {
        if (request.MemberIds.Count == 0) return BadRequest(new ProblemDetails { Title = "Pick at least one member." });
        if (request.MemberIds.Count > 50)
            return BadRequest(new ProblemDetails { Title = "Fifty at a time. More than that is a campaign, not a win-back." });

        var options = new WinBackOptions
        {
            DiscountPercent = Math.Clamp(request.DiscountPercent, 0m, 60m),
            MaxDiscountAmount = request.MaxDiscountAmount,
            OfferValidDays = Math.Clamp(request.OfferValidDays, 3, 90),
            Message = request.Message,
            SendWhatsApp = request.SendWhatsApp,
            SendEmail = request.SendEmail,
            Force = request.Force
        };

        var sent = new List<object>();
        var skipped = new List<object>();
        var actor = User.Identity?.Name ?? "admin";

        foreach (var memberId in request.MemberIds.Distinct())
        {
            var result = await _churn.RunWinBackAsync(memberId, options, actor, ct);
            if (result.Sent) sent.Add(new { memberId, result.Message, result.CouponCode });
            else skipped.Add(new { memberId, reason = result.Message });
        }

        return Ok(new { sent = sent.Count, skipped = skipped.Count, details = new { sent, skipped } });
    }
}

public record WinBackRequest
{
    public decimal DiscountPercent { get; init; } = 20m;
    public decimal? MaxDiscountAmount { get; init; }
    public int OfferValidDays { get; init; } = 14;
    public string? Message { get; init; }
    public bool SendWhatsApp { get; init; } = true;
    public bool SendEmail { get; init; } = true;
    public bool Force { get; init; }
}

public record BulkWinBackRequest : WinBackRequest
{
    public IReadOnlyList<int> MemberIds { get; init; } = Array.Empty<int>();
}

public record ChurnRadarResponse
{
    public DateTime? ScoredAtUtc { get; init; }
    public int Healthy { get; init; }
    public int Watch { get; init; }
    public int Amber { get; init; }
    public int Red { get; init; }
    public decimal RevenueAtRisk { get; init; }
    public required IReadOnlyList<ChurnRadarRow> Rows { get; init; }
}

public record ChurnRadarRow
{
    public required int MemberId { get; init; }
    public required string MemberCode { get; init; }
    public required string FullName { get; init; }
    public required string Phone { get; init; }
    public string? Email { get; init; }
    public string? PhotoUrl { get; init; }
    public required int BranchId { get; init; }
    public required string BranchName { get; init; }
    public required ChurnRiskBand Band { get; init; }
    public required int Score { get; init; }
    public required IReadOnlyList<string> Reasons { get; init; }
    public string? LastVisitOn { get; init; }
    public int? DaysSinceVisit { get; init; }
    public int CurrentStreakDays { get; init; }
    public required MemberStatus Status { get; init; }
    public string? PlanName { get; init; }
    public string? PlanEndsOn { get; init; }
    public decimal PlanValue { get; init; }
    public decimal AmountDue { get; init; }
    public DateTime? LastWinBackAtUtc { get; init; }
}
