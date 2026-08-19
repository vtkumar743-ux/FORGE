using System.Text.Json;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers.Portal;

/// <summary>
/// The community side of the portal (Module 4.5, 4.4 and 4.6): the feed personal records post
/// into, the share card a member sends to WhatsApp, the body-scan PDF, and corporate
/// self-enrolment.
/// </summary>
[Route("api/portal/community")]
public class PortalCommunityController : PortalControllerBase
{
    private readonly GymDbContext _db;
    private readonly ProgressReportService _reports;
    private readonly CorporateService _corporate;
    private readonly IClock _clock;

    public PortalCommunityController(
        GymDbContext db, ProgressReportService reports, CorporateService corporate, IClock clock)
    {
        _db = db;
        _reports = reports;
        _corporate = corporate;
        _clock = clock;
    }

    // ================================================================== feed

    /// <summary>
    /// The branch feed. Members who have not opted into the leaderboard never appear in it —
    /// the consent is checked when the post is written, and again here, because a member who
    /// withdraws consent expects their name to come down today, not from tomorrow's posts.
    /// </summary>
    [HttpGet("feed")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Feed(
        [FromQuery] string scope = "branch", [FromQuery] int take = 30, CancellationToken ct = default)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var me = await _db.Members.AsNoTracking()
            .Where(m => m.Id == memberId)
            .Select(m => new { m.HomeBranchId, m.ConsentLeaderboard })
            .FirstAsync(ct);

        var query = _db.FeedPosts.AsNoTracking()
            .Where(p => p.IsVisible)
            .Where(p => p.Member == null || p.Member.ConsentLeaderboard);

        if (!string.Equals(scope, "network", StringComparison.OrdinalIgnoreCase))
            query = query.Where(p => p.BranchId == null || p.BranchId == me.HomeBranchId);

        var posts = await query
            .OrderByDescending(p => p.IsPinned)
            .ThenByDescending(p => p.PostedAtUtc)
            .Take(Math.Clamp(take, 1, 60))
            .Select(p => new
            {
                p.Id, p.Kind, p.Title, p.Body, p.ImageUrl, p.MetaJson, p.LikeCount, p.IsPinned, p.PostedAtUtc,
                p.MemberId,
                MemberName = p.Member == null ? null : p.Member.FullName,
                MemberPhoto = p.Member == null ? null : p.Member.PhotoUrl,
                BranchName = p.Branch == null ? null : p.Branch.Name
            })
            .ToListAsync(ct);

        return Ok(new
        {
            consentGiven = me.ConsentLeaderboard,
            // Said plainly rather than hidden in a settings screen: a member browsing a feed
            // they are absent from should know why, and how to join it.
            consentPrompt = me.ConsentLeaderboard
                ? null
                : "Your records are private. Turn on leaderboard sharing in your profile to post them here.",
            posts = posts.Select(p => new
            {
                p.Id,
                kind = p.Kind.ToString(),
                p.Title,
                p.Body,
                p.ImageUrl,
                p.LikeCount,
                p.IsPinned,
                p.PostedAtUtc,
                isMine = p.MemberId == memberId,
                authorName = p.MemberName,
                authorPhotoUrl = p.MemberPhoto,
                p.BranchName,
                meta = ParseMeta(p.MetaJson),
                ago = Ago(_clock.UtcNow - p.PostedAtUtc)
            })
        });
    }

    /// <summary>
    /// A like. Deliberately not a full reactions system: one counter, no per-member row, so
    /// the feed stays a noticeboard rather than becoming something to moderate.
    /// </summary>
    [HttpPost("feed/{id:int}/like")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Like(int id, CancellationToken ct)
    {
        if (CurrentMemberId is null) return NoMemberProfile();

        var post = await _db.FeedPosts.FirstOrDefaultAsync(p => p.Id == id && p.IsVisible, ct);
        if (post is null) return NotFound();

        post.LikeCount += 1;
        await _db.SaveChangesAsync(ct);
        return Ok(new { post.Id, post.LikeCount });
    }

    /// <summary>
    /// The WhatsApp share card for a personal record. The server composes the text so the
    /// wording is the same everywhere it is shared and the numbers cannot drift from the log.
    /// </summary>
    [HttpGet("share/pr/{workoutLogId:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> PrShareCard(int workoutLogId, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var log = await _db.WorkoutLogs.AsNoTracking()
            .Include(l => l.Exercise)
            .Include(l => l.Member).ThenInclude(m => m.HomeBranch)
            .FirstOrDefaultAsync(l => l.Id == workoutLogId && l.MemberId == memberId, ct);

        // Not-found rather than forbidden: "that exists but is not yours" is itself a leak.
        if (log is null || !log.IsPersonalRecord) return NotFound();

        var previousBest = await _db.WorkoutLogs.AsNoTracking()
            .Where(l => l.MemberId == memberId && l.ExerciseId == log.ExerciseId && l.Id != log.Id
                        && l.PerformedAtUtc < log.PerformedAtUtc)
            .MaxAsync(l => (decimal?)l.EstimatedOneRepMax, ct);

        var gain = previousBest is { } prev && prev > 0 ? log.EstimatedOneRepMax - prev : (decimal?)null;
        var firstName = log.Member.FullName.Split(' ')[0];

        var text = gain is { } g and > 0
            ? $"New PR: {log.Exercise.Name} {log.WeightKg:0.#} kg x {log.Reps}. That is {g:0.#} kg on my best, at FORGE {log.Member.HomeBranch.Name}."
            : $"New PR: {log.Exercise.Name} {log.WeightKg:0.#} kg x {log.Reps} at FORGE {log.Member.HomeBranch.Name}.";

        return Ok(new
        {
            memberName = firstName,
            exercise = log.Exercise.Name,
            weightKg = log.WeightKg,
            reps = log.Reps,
            estimatedOneRepMax = Math.Round(log.EstimatedOneRepMax, 1),
            previousBest = previousBest is null ? null : (decimal?)Math.Round(previousBest.Value, 1),
            gainKg = gain is null ? null : (decimal?)Math.Round(gain.Value, 1),
            performedOn = log.PerformedOn.ToString("yyyy-MM-dd"),
            branchName = log.Member.HomeBranch.Name,
            shareText = text,
            whatsAppUrl = $"https://wa.me/?text={Uri.EscapeDataString(text)}"
        });
    }

    // ================================================================== report

    /// <summary>
    /// The shareable body-scan report as a PDF (Module 4.4). Streamed rather than stored: the
    /// figures change every time a scan is added, so a cached file would go stale on disk.
    /// </summary>
    [HttpGet("progress-report.pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ProgressReport(CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var result = await _reports.BuildBodyScanReportAsync(memberId, ct: ct);
        if (result is null) return NotFound();

        Response.Headers.CacheControl = "private,no-store";
        return File(result.Value.Pdf, "application/pdf", result.Value.FileName);
    }

    // ================================================================== corporate

    /// <summary>Checks a company code and says what it is worth, without committing to it.</summary>
    [HttpGet("corporate/preview")]
    [ProducesResponseType(typeof(CorporateCodeResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewCorporate([FromQuery] string code, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var branchId = await _db.Members.AsNoTracking()
            .Where(m => m.Id == memberId).Select(m => m.HomeBranchId).FirstAsync(ct);

        var result = await _corporate.PreviewAsync(code, branchId, ct);
        return Ok(result);
    }

    [HttpPost("corporate/enrol")]
    [ProducesResponseType(typeof(CorporateCodeResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EnrolCorporate(PortalEnrolRequest request, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var result = await _corporate.EnrolAsync(memberId, request.Code, request.EmployeeId, request.WorkEmail, ct);
        return result.Accepted ? Ok(result) : Conflict(result);
    }

    /// <summary>The member's own corporate standing, for the membership screen.</summary>
    [HttpGet("corporate/mine")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> MyCorporate(CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var enrolment = await _db.CorporateEnrolments.AsNoTracking()
            .Include(e => e.CorporateAccount)
            .Where(e => e.MemberId == memberId && e.IsActive)
            .OrderByDescending(e => e.EnrolledOn)
            .FirstOrDefaultAsync(ct);

        if (enrolment is null) return Ok(new { enrolled = false });

        return Ok(new
        {
            enrolled = true,
            enrolment.Id,
            companyName = enrolment.CorporateAccount.CompanyName,
            code = enrolment.CorporateAccount.Code,
            discountPercent = enrolment.CorporateAccount.DiscountPercent,
            waiveAdmissionFee = enrolment.CorporateAccount.WaiveAdmissionFee,
            enrolledOn = enrolment.EnrolledOn.ToString("yyyy-MM-dd"),
            validTo = enrolment.CorporateAccount.ValidTo.ToString("yyyy-MM-dd"),
            enrolment.EmployeeId
        });
    }

    // ================================================================== helpers

    private static object? ParseMeta(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        }
        catch (JsonException)
        {
            // A malformed meta blob is a missing badge on one card, not a broken feed.
            return null;
        }
    }

    private static string Ago(TimeSpan span) => span switch
    {
        { TotalMinutes: < 2 } => "just now",
        { TotalMinutes: < 60 } => $"{(int)span.TotalMinutes} min ago",
        { TotalHours: < 24 } => $"{(int)span.TotalHours} h ago",
        { TotalDays: < 7 } => $"{(int)span.TotalDays} d ago",
        _ => $"{(int)(span.TotalDays / 7)} w ago"
    };
}

public record PortalEnrolRequest
{
    public required string Code { get; init; }
    public string? EmployeeId { get; init; }
    public string? WorkEmail { get; init; }
}
