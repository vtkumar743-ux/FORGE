using Gym.Api.Contracts;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers.Admin;

/// <summary>
/// The CMS-managed collections the public site reads from: testimonials, transformations,
/// FAQs and the journal. These are rows rather than section content because several sections
/// draw from the same pool — editing a testimonial once updates every wall that shows it.
/// </summary>
[ApiController]
[Route("api/admin/content")]
[Authorize(Roles = RoleNames.Admin)]
[Produces("application/json")]
public class AdminCollectionsController : ControllerBase
{
    private readonly GymDbContext _db;

    public AdminCollectionsController(GymDbContext db) => _db = db;

    // ------------------------------------------------------------------ testimonials

    [HttpGet("testimonials")]
    public async Task<ActionResult<IReadOnlyList<object>>> Testimonials(CancellationToken ct) =>
        Ok(await _db.Testimonials
            .AsNoTracking()
            .OrderBy(t => t.DisplayOrder).ThenByDescending(t => t.Id)
            .Select(t => new
            {
                t.Id, t.AuthorName, t.AuthorRole, t.AuthorPhotoUrl, t.Quote, t.Rating,
                t.BranchId, BranchName = t.Branch != null ? t.Branch.Name : null,
                t.Program, t.GoogleReviewUrl, t.IsFeatured, t.IsVisible, t.DisplayOrder
            })
            .ToListAsync(ct));

    [HttpPost("testimonials")]
    public async Task<IActionResult> CreateTestimonial(TestimonialRequest request, CancellationToken ct)
    {
        var row = new Testimonial { CreatedBy = User.Identity?.Name };
        Apply(row, request);
        _db.Testimonials.Add(row);
        await _db.SaveChangesAsync(ct);
        return Created($"/api/admin/content/testimonials/{row.Id}", new { row.Id });
    }

    [HttpPut("testimonials/{id:int}")]
    public async Task<IActionResult> UpdateTestimonial(int id, TestimonialRequest request, CancellationToken ct)
    {
        var row = await _db.Testimonials.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (row is null) return NotFound();
        Apply(row, request);
        row.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("testimonials/{id:int}")]
    public async Task<IActionResult> DeleteTestimonial(int id, CancellationToken ct)
    {
        var row = await _db.Testimonials.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (row is null) return NotFound();
        _db.Testimonials.Remove(row);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static void Apply(Testimonial row, TestimonialRequest r)
    {
        row.AuthorName = r.AuthorName.Trim();
        row.AuthorRole = r.AuthorRole;
        row.AuthorPhotoUrl = r.AuthorPhotoUrl;
        row.Quote = r.Quote.Trim();
        row.Rating = Math.Clamp(r.Rating, 1, 5);
        row.BranchId = r.BranchId;
        row.Program = r.Program;
        row.GoogleReviewUrl = r.GoogleReviewUrl;
        row.IsFeatured = r.IsFeatured;
        row.IsVisible = r.IsVisible;
        row.DisplayOrder = r.DisplayOrder;
    }

    // ------------------------------------------------------------------ transformations

    [HttpGet("transformations")]
    public async Task<ActionResult<IReadOnlyList<object>>> Transformations(CancellationToken ct) =>
        Ok(await _db.Transformations
            .AsNoTracking()
            .OrderBy(t => t.DisplayOrder).ThenByDescending(t => t.Id)
            .Select(t => new
            {
                t.Id, t.MemberDisplayName, t.MemberId, t.BeforeImageUrl, t.AfterImageUrl,
                t.DurationWeeks, t.Program, t.TrainerName, t.WeightBeforeKg, t.WeightAfterKg,
                t.Story, t.BranchId, BranchName = t.Branch != null ? t.Branch.Name : null,
                t.ConsentGiven, t.ConsentAtUtc, t.IsVisible, t.DisplayOrder
            })
            .ToListAsync(ct));

    [HttpPost("transformations")]
    public async Task<IActionResult> CreateTransformation(TransformationRequest request, CancellationToken ct)
    {
        var row = new Transformation { CreatedBy = User.Identity?.Name };
        Apply(row, request);
        _db.Transformations.Add(row);
        await _db.SaveChangesAsync(ct);
        return Created($"/api/admin/content/transformations/{row.Id}", new { row.Id });
    }

    [HttpPut("transformations/{id:int}")]
    public async Task<IActionResult> UpdateTransformation(int id, TransformationRequest request, CancellationToken ct)
    {
        var row = await _db.Transformations.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (row is null) return NotFound();
        Apply(row, request);
        row.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("transformations/{id:int}")]
    public async Task<IActionResult> DeleteTransformation(int id, CancellationToken ct)
    {
        var row = await _db.Transformations.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (row is null) return NotFound();
        _db.Transformations.Remove(row);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private void Apply(Transformation row, TransformationRequest r)
    {
        row.MemberDisplayName = r.MemberDisplayName.Trim();
        row.MemberId = r.MemberId;
        row.BeforeImageUrl = r.BeforeImageUrl;
        row.AfterImageUrl = r.AfterImageUrl;
        row.DurationWeeks = r.DurationWeeks;
        row.Program = r.Program;
        row.TrainerName = r.TrainerName;
        row.WeightBeforeKg = r.WeightBeforeKg;
        row.WeightAfterKg = r.WeightAfterKg;
        row.Story = r.Story;
        row.BranchId = r.BranchId;
        row.IsVisible = r.IsVisible;
        row.DisplayOrder = r.DisplayOrder;

        // Consent is a hard gate and it is timestamped the moment it is granted — the gallery
        // publishes a named person with their weight, so "who ticked this and when" matters.
        if (r.ConsentGiven && !row.ConsentGiven)
        {
            row.ConsentGiven = true;
            row.ConsentAtUtc = DateTime.UtcNow;
        }
        else if (!r.ConsentGiven)
        {
            row.ConsentGiven = false;
            row.ConsentAtUtc = null;
        }
    }

    // ------------------------------------------------------------------ faqs

    [HttpGet("faqs")]
    public async Task<ActionResult<IReadOnlyList<object>>> Faqs(CancellationToken ct) =>
        Ok(await _db.FaqItems
            .AsNoTracking()
            .OrderBy(f => f.Category).ThenBy(f => f.DisplayOrder)
            .Select(f => new
            {
                f.Id, f.Question, f.Answer, f.Category, f.BranchId,
                BranchName = f.Branch != null ? f.Branch.Name : null,
                f.IsVisible, f.DisplayOrder
            })
            .ToListAsync(ct));

    [HttpPost("faqs")]
    public async Task<IActionResult> CreateFaq(FaqRequest request, CancellationToken ct)
    {
        var row = new FaqItem { CreatedBy = User.Identity?.Name };
        Apply(row, request);
        _db.FaqItems.Add(row);
        await _db.SaveChangesAsync(ct);
        return Created($"/api/admin/content/faqs/{row.Id}", new { row.Id });
    }

    [HttpPut("faqs/{id:int}")]
    public async Task<IActionResult> UpdateFaq(int id, FaqRequest request, CancellationToken ct)
    {
        var row = await _db.FaqItems.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (row is null) return NotFound();
        Apply(row, request);
        row.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("faqs/{id:int}")]
    public async Task<IActionResult> DeleteFaq(int id, CancellationToken ct)
    {
        var row = await _db.FaqItems.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (row is null) return NotFound();
        _db.FaqItems.Remove(row);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static void Apply(FaqItem row, FaqRequest r)
    {
        row.Question = r.Question.Trim();
        row.Answer = r.Answer.Trim();
        row.Category = r.Category.Trim();
        row.BranchId = r.BranchId;
        row.IsVisible = r.IsVisible;
        row.DisplayOrder = r.DisplayOrder;
    }

    // ------------------------------------------------------------------ journal

    [HttpGet("posts")]
    public async Task<ActionResult<IReadOnlyList<object>>> Posts(CancellationToken ct) =>
        Ok(await _db.BlogPosts
            .AsNoTracking()
            .OrderByDescending(p => p.PublishedAtUtc ?? p.CreatedAtUtc)
            .Select(p => new
            {
                p.Id, p.Slug, p.Title, p.Excerpt, p.CoverImageUrl, p.AuthorName, p.AuthorRole,
                p.Tags, p.ReadMinutes, p.State, p.PublishedAtUtc, p.IsFeatured, p.ViewCount
            })
            .ToListAsync(ct));

    [HttpGet("posts/{id:int}")]
    public async Task<IActionResult> Post(int id, CancellationToken ct)
    {
        var post = await _db.BlogPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (post is null) return NotFound();

        return Ok(new
        {
            post.Id, post.Slug, post.Title, post.Excerpt,
            Body = CmsSectionResponse.Parse(post.BodyJson),
            post.CoverImageUrl, post.AuthorName, post.AuthorRole, post.Tags, post.ReadMinutes,
            post.SeoTitle, post.SeoDescription, post.OgImageUrl, post.State, post.PublishedAtUtc,
            post.IsFeatured, post.ViewCount
        });
    }

    [HttpPost("posts")]
    public async Task<IActionResult> CreatePost(BlogPostRequest request, CancellationToken ct)
    {
        var slug = AdminCmsController.Slugify(request.Slug);
        if (await _db.BlogPosts.AnyAsync(p => p.Slug == slug, ct))
        {
            ModelState.AddModelError(nameof(request.Slug), "A journal post already uses that slug.");
            return ValidationProblem(ModelState);
        }

        var post = new BlogPost { Slug = slug, CreatedBy = User.Identity?.Name };
        Apply(post, request);
        _db.BlogPosts.Add(post);
        await _db.SaveChangesAsync(ct);
        return Created($"/api/admin/content/posts/{post.Id}", new { post.Id, post.Slug });
    }

    [HttpPut("posts/{id:int}")]
    public async Task<IActionResult> UpdatePost(int id, BlogPostRequest request, CancellationToken ct)
    {
        var post = await _db.BlogPosts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (post is null) return NotFound();

        var slug = AdminCmsController.Slugify(request.Slug);
        if (slug != post.Slug && await _db.BlogPosts.AnyAsync(p => p.Slug == slug && p.Id != id, ct))
        {
            ModelState.AddModelError(nameof(request.Slug), "A journal post already uses that slug.");
            return ValidationProblem(ModelState);
        }

        post.Slug = slug;
        Apply(post, request);
        post.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("posts/{id:int}")]
    public async Task<IActionResult> DeletePost(int id, CancellationToken ct)
    {
        var post = await _db.BlogPosts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (post is null) return NotFound();
        _db.BlogPosts.Remove(post);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static void Apply(BlogPost post, BlogPostRequest r)
    {
        post.Title = r.Title.Trim();
        post.Excerpt = r.Excerpt.Trim();
        if (r.Body is not null) post.BodyJson = r.Body.ToJsonString();
        post.CoverImageUrl = r.CoverImageUrl;
        post.AuthorName = r.AuthorName.Trim();
        post.AuthorRole = r.AuthorRole;
        post.Tags = r.Tags;
        post.ReadMinutes = r.ReadMinutes;
        post.SeoTitle = string.IsNullOrWhiteSpace(r.SeoTitle) ? r.Title : r.SeoTitle;
        post.SeoDescription = string.IsNullOrWhiteSpace(r.SeoDescription) ? r.Excerpt : r.SeoDescription;
        post.OgImageUrl = r.OgImageUrl ?? r.CoverImageUrl;
        post.IsFeatured = r.IsFeatured;

        if (r.State == PublishState.Published && post.State != PublishState.Published)
            post.PublishedAtUtc = DateTime.UtcNow;
        post.State = r.State;
    }
}
