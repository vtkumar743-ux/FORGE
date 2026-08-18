using System.Text.Json;
using Gym.Api.Contracts;
using Gym.Core.Enums;
using Gym.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers;

/// <summary>
/// The CMS-managed content lists the public sections read from: testimonials (1.8),
/// transformations (1.7), FAQs and the journal (1.9). These are separate tables rather
/// than section JSON because the owner curates them once and reuses them across pages.
/// </summary>
[ApiController]
[Route("api/content")]
[Produces("application/json")]
[AllowAnonymous]
public class ContentController : ControllerBase
{
    private readonly GymDbContext _db;

    public ContentController(GymDbContext db) => _db = db;

    [HttpGet("testimonials")]
    [ProducesResponseType(typeof(IReadOnlyList<TestimonialResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TestimonialResponse>>> Testimonials(
        [FromQuery] bool featuredOnly, [FromQuery] string? branchSlug, [FromQuery] int? limit, CancellationToken ct)
    {
        var query = _db.Testimonials
            .AsNoTracking()
            .Where(t => t.IsVisible)
            .Where(t => !featuredOnly || t.IsFeatured)
            .Where(t => branchSlug == null || (t.Branch != null && t.Branch.Slug == branchSlug))
            .OrderBy(t => t.DisplayOrder)
            .Select(t => new TestimonialResponse
            {
                Id = t.Id,
                AuthorName = t.AuthorName,
                AuthorRole = t.AuthorRole,
                AuthorPhotoUrl = t.AuthorPhotoUrl,
                Quote = t.Quote,
                Rating = t.Rating,
                Program = t.Program,
                BranchName = t.Branch != null ? t.Branch.Name : null,
                BranchSlug = t.Branch != null ? t.Branch.Slug : null,
                IsFeatured = t.IsFeatured
            });

        if (limit is > 0) query = query.Take(limit.Value);
        return Ok(await query.ToListAsync(ct));
    }

    /// <summary>Consent is a hard gate — an un-consented row can never reach this endpoint.</summary>
    [HttpGet("transformations")]
    [ProducesResponseType(typeof(IReadOnlyList<TransformationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TransformationResponse>>> Transformations(
        [FromQuery] string? branchSlug, [FromQuery] int? limit, CancellationToken ct)
    {
        var query = _db.Transformations
            .AsNoTracking()
            .Where(t => t.IsVisible && t.ConsentGiven)
            .Where(t => branchSlug == null || (t.Branch != null && t.Branch.Slug == branchSlug))
            .OrderBy(t => t.DisplayOrder)
            .Select(t => new TransformationResponse
            {
                Id = t.Id,
                MemberDisplayName = t.MemberDisplayName,
                BeforeImageUrl = t.BeforeImageUrl,
                AfterImageUrl = t.AfterImageUrl,
                DurationWeeks = t.DurationWeeks,
                Program = t.Program,
                TrainerName = t.TrainerName,
                WeightBeforeKg = t.WeightBeforeKg,
                WeightAfterKg = t.WeightAfterKg,
                Story = t.Story,
                BranchName = t.Branch != null ? t.Branch.Name : null,
                BranchSlug = t.Branch != null ? t.Branch.Slug : null
            });

        if (limit is > 0) query = query.Take(limit.Value);
        return Ok(await query.ToListAsync(ct));
    }

    [HttpGet("faqs")]
    [ProducesResponseType(typeof(IReadOnlyList<FaqResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FaqResponse>>> Faqs(
        [FromQuery] string? category, [FromQuery] string? branchSlug, CancellationToken ct)
    {
        var faqs = await _db.FaqItems
            .AsNoTracking()
            .Where(f => f.IsVisible)
            .Where(f => category == null || f.Category == category)
            .Where(f => f.BranchId == null || (branchSlug != null && f.Branch!.Slug == branchSlug))
            .OrderBy(f => f.DisplayOrder)
            .Select(f => new FaqResponse
            {
                Id = f.Id,
                Category = f.Category,
                Question = f.Question,
                Answer = f.Answer,
                DisplayOrder = f.DisplayOrder
            })
            .ToListAsync(ct);

        return Ok(faqs);
    }

    [HttpGet("journal")]
    [ProducesResponseType(typeof(IReadOnlyList<BlogSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BlogSummaryResponse>>> Journal(
        [FromQuery] string? tag, [FromQuery] int? limit, CancellationToken ct)
    {
        var posts = await PublishedPosts()
            .Select(p => new
            {
                p.Id, p.Slug, p.Title, p.Excerpt, p.CoverImageUrl, p.AuthorName, p.AuthorRole,
                p.Tags, p.ReadMinutes, p.PublishedAtUtc, p.IsFeatured
            })
            .ToListAsync(ct);

        var mapped = posts.Select(p => new BlogSummaryResponse
        {
            Id = p.Id,
            Slug = p.Slug,
            Title = p.Title,
            Excerpt = p.Excerpt,
            CoverImageUrl = p.CoverImageUrl,
            AuthorName = p.AuthorName,
            AuthorRole = p.AuthorRole,
            Tags = ClassesController.Split(p.Tags),
            ReadMinutes = p.ReadMinutes,
            PublishedAtUtc = p.PublishedAtUtc,
            IsFeatured = p.IsFeatured
        }).ToList();

        if (!string.IsNullOrWhiteSpace(tag))
            mapped = mapped.Where(p => p.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList();
        if (limit is > 0)
            mapped = mapped.Take(limit.Value).ToList();

        return Ok(mapped);
    }

    [HttpGet("journal/{slug}")]
    [ProducesResponseType(typeof(BlogPostResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BlogPostResponse>> JournalPost(string slug, CancellationToken ct)
    {
        var post = await PublishedPosts().FirstOrDefaultAsync(p => p.Slug == slug, ct);
        if (post is null) return NotFound();

        var tags = ClassesController.Split(post.Tags);

        var others = await PublishedPosts()
            .Where(p => p.Id != post.Id)
            .Select(p => new
            {
                p.Id, p.Slug, p.Title, p.Excerpt, p.CoverImageUrl, p.AuthorName, p.AuthorRole,
                p.Tags, p.ReadMinutes, p.PublishedAtUtc, p.IsFeatured
            })
            .ToListAsync(ct);

        // Related = shares a tag, newest first; topped up with recent posts so the rail is never short.
        var related = others
            .OrderByDescending(p => ClassesController.Split(p.Tags).Count(t => tags.Contains(t, StringComparer.OrdinalIgnoreCase)))
            .ThenByDescending(p => p.PublishedAtUtc)
            .Take(3)
            .Select(p => new BlogSummaryResponse
            {
                Id = p.Id, Slug = p.Slug, Title = p.Title, Excerpt = p.Excerpt,
                CoverImageUrl = p.CoverImageUrl, AuthorName = p.AuthorName, AuthorRole = p.AuthorRole,
                Tags = ClassesController.Split(p.Tags), ReadMinutes = p.ReadMinutes,
                PublishedAtUtc = p.PublishedAtUtc, IsFeatured = p.IsFeatured
            })
            .ToList();

        return Ok(new BlogPostResponse
        {
            Id = post.Id,
            Slug = post.Slug,
            Title = post.Title,
            Excerpt = post.Excerpt,
            CoverImageUrl = post.CoverImageUrl,
            AuthorName = post.AuthorName,
            AuthorRole = post.AuthorRole,
            Tags = tags,
            ReadMinutes = post.ReadMinutes,
            PublishedAtUtc = post.PublishedAtUtc,
            IsFeatured = post.IsFeatured,
            Body = ParseBody(post.BodyJson),
            SeoTitle = post.SeoTitle,
            SeoDescription = post.SeoDescription,
            OgImageUrl = post.OgImageUrl ?? post.CoverImageUrl,
            Related = related
        });
    }

    private IQueryable<Core.Entities.BlogPost> PublishedPosts() =>
        _db.BlogPosts
            .AsNoTracking()
            .Where(p => p.State == PublishState.Published)
            .OrderByDescending(p => p.PublishedAtUtc);

    /// <summary>
    /// Body is structured blocks, not HTML. A post whose JSON has been mangled degrades to
    /// its excerpt rather than rendering broken markup or taking the route down.
    /// </summary>
    private static IReadOnlyList<BlogBlock> ParseBody(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<BlogBlock>>(
                json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<BlogBlock>();
        }
        catch (JsonException)
        {
            return Array.Empty<BlogBlock>();
        }
    }
}
