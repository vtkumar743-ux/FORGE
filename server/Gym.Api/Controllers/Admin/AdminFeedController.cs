using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers.Admin;

/// <summary>
/// The community feed from the owner's side (Module 4.5): what has been posted, an
/// announcement they can pin, and the ability to take a post down. Personal records post
/// themselves — this is the moderation and the megaphone, not an authoring tool.
/// </summary>
[ApiController]
[Route("api/admin/feed")]
[Authorize(Roles = RoleNames.Admin)]
[Produces("application/json")]
public class AdminFeedController : ControllerBase
{
    private readonly GymDbContext _db;
    private readonly IClock _clock;

    public AdminFeedController(GymDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> All(
        [FromQuery] int? branchId, [FromQuery] bool includeHidden = true,
        [FromQuery] int take = 60, CancellationToken ct = default)
    {
        var query = _db.FeedPosts.AsNoTracking();
        if (branchId is { } b) query = query.Where(p => p.BranchId == b);
        if (!includeHidden) query = query.Where(p => p.IsVisible);

        var posts = await query
            .OrderByDescending(p => p.IsPinned)
            .ThenByDescending(p => p.PostedAtUtc)
            .Take(Math.Clamp(take, 1, 200))
            .Select(p => new
            {
                p.Id, Kind = p.Kind.ToString(), p.Title, p.Body, p.ImageUrl, p.LikeCount,
                p.IsPinned, p.IsVisible, p.PostedAtUtc, p.MetaJson,
                MemberId = p.MemberId,
                MemberName = p.Member == null ? null : p.Member.FullName,
                MemberCode = p.Member == null ? null : p.Member.MemberCode,
                BranchName = p.Branch == null ? null : p.Branch.Name,
                ConsentGiven = p.Member == null ? (bool?)null : p.Member.ConsentLeaderboard
            })
            .ToListAsync(ct);

        var week = _clock.UtcNow.AddDays(-7);
        return Ok(new
        {
            prsThisWeek = await _db.FeedPosts.CountAsync(
                p => p.Kind == FeedPostKind.PersonalRecord && p.PostedAtUtc >= week, ct),
            hidden = await _db.FeedPosts.CountAsync(p => !p.IsVisible, ct),
            posts
        });
    }

    /// <summary>An announcement from the gym. Pinned posts sit above the records.</summary>
    [HttpPost("announce")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    public async Task<IActionResult> Announce(AnnouncementRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new ProblemDetails { Title = "An announcement needs a headline." });

        if (request.Pin)
        {
            // One pin at a time — a feed with four pinned posts has none.
            var pinned = await _db.FeedPosts.Where(p => p.IsPinned).ToListAsync(ct);
            foreach (var post in pinned) post.IsPinned = false;
        }

        var announcement = new FeedPost
        {
            Kind = FeedPostKind.Announcement,
            BranchId = request.BranchId,
            Title = request.Title.Trim(),
            Body = string.IsNullOrWhiteSpace(request.Body) ? null : request.Body.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
            IsPinned = request.Pin,
            IsVisible = true,
            PostedAtUtc = _clock.UtcNow,
            CreatedBy = User.Identity?.Name
        };

        _db.FeedPosts.Add(announcement);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(All), new { id = announcement.Id }, new { announcement.Id });
    }

    [HttpPost("{id:int}/visibility")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Visibility(int id, VisibilityRequest request, CancellationToken ct)
    {
        var post = await _db.FeedPosts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (post is null) return NotFound();

        post.IsVisible = request.Visible;
        if (!request.Visible) post.IsPinned = false;
        post.UpdatedAtUtc = _clock.UtcNow;
        post.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync(ct);

        return Ok(new { post.Id, post.IsVisible });
    }

    [HttpPost("{id:int}/pin")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Pin(int id, PinRequest request, CancellationToken ct)
    {
        var post = await _db.FeedPosts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (post is null) return NotFound();

        if (request.Pinned)
        {
            var pinned = await _db.FeedPosts.Where(p => p.IsPinned && p.Id != id).ToListAsync(ct);
            foreach (var other in pinned) other.IsPinned = false;
            post.IsVisible = true;
        }

        post.IsPinned = request.Pinned;
        await _db.SaveChangesAsync(ct);
        return Ok(new { post.Id, post.IsPinned });
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var post = await _db.FeedPosts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (post is null) return NotFound();

        // Announcements are the gym's own copy and can go. A member's record is their
        // achievement — it is hidden, never deleted, so the history behind it stays intact.
        if (post.Kind != FeedPostKind.Announcement)
            return Conflict(new ProblemDetails
            {
                Title = "Records are hidden, not deleted",
                Detail = "Hide the post instead — the member's logged record stays either way."
            });

        _db.Remove(post);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public record AnnouncementRequest
{
    public required string Title { get; init; }
    public string? Body { get; init; }
    public string? ImageUrl { get; init; }
    public int? BranchId { get; init; }
    public bool Pin { get; init; }
}

public record VisibilityRequest
{
    public bool Visible { get; init; }
}

public record PinRequest
{
    public bool Pinned { get; init; }
}
