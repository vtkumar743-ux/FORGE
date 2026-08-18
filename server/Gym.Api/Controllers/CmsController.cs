using Gym.Api.Contracts;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers;

/// <summary>
/// Public reads are anonymous and serve published content only; writes require Admin and go
/// to the draft slot until explicitly published. Every public page renders from here, which
/// is what makes "hardcoded marketing copy is a bug" enforceable.
/// </summary>
[ApiController]
[Route("api/cms")]
[Produces("application/json")]
public class CmsController : ControllerBase
{
    private readonly GymDbContext _db;
    private readonly ILogger<CmsController> _log;

    public CmsController(GymDbContext db, ILogger<CmsController> log)
    {
        _db = db;
        _log = log;
    }

    /// <summary>Every published page slug — used for the sitemap and the admin page list.</summary>
    [HttpGet("pages")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Pages(CancellationToken ct)
    {
        var pages = await _db.CmsPages
            .AsNoTracking()
            .Where(p => p.State == PublishState.Published)
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new
            {
                p.Id, p.Slug, p.Title, p.DisplayOrder, p.IsSystemPage,
                SectionCount = p.Sections.Count(s => s.IsVisible)
            })
            .ToListAsync(ct);

        return Ok(pages);
    }

    /// <summary>
    /// One page with its ordered sections. <paramref name="branchSlug"/> selects branch-scoped
    /// section variants; <paramref name="preview"/> (Admin only) returns draft content instead.
    /// </summary>
    [HttpGet("pages/{*slug}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CmsPageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CmsPageResponse>> Page(
        string slug, [FromQuery] string? branchSlug, [FromQuery] bool preview, CancellationToken ct)
    {
        var isAdmin = User.IsInRole(RoleNames.Admin);
        if (preview && !isAdmin) preview = false;

        var page = await _db.CmsPages
            .AsNoTracking()
            .Include(p => p.Sections)
            .FirstOrDefaultAsync(p => p.Slug == slug, ct);

        if (page is null) return NotFound();
        if (page.State != PublishState.Published && !isAdmin) return NotFound();

        int? branchId = null;
        if (!string.IsNullOrWhiteSpace(branchSlug))
            branchId = await _db.Branches.AsNoTracking()
                .Where(b => b.Slug == branchSlug)
                .Select(b => (int?)b.Id)
                .FirstOrDefaultAsync(ct);

        var sections = page.Sections
            .Where(s => preview || s.IsVisible)
            .Where(s => preview || s.State == PublishState.Published)
            // Keep shared sections plus the variants for the requested branch, never another's.
            .Where(s => s.BranchId is null || s.BranchId == branchId)
            .OrderBy(s => s.OrderIndex)
            .Select(s => ToResponse(s, preview))
            .ToList();

        return Ok(new CmsPageResponse
        {
            Id = page.Id,
            Slug = page.Slug,
            Title = page.Title,
            Seo = new CmsSeoResponse
            {
                Title = page.SeoTitle,
                Description = page.SeoDescription,
                Keywords = page.SeoKeywords,
                OgImageUrl = page.OgImageUrl,
                CanonicalUrl = page.CanonicalUrl,
                NoIndex = page.NoIndex,
                StructuredData = page.StructuredDataJson is null
                    ? null
                    : CmsSectionResponse.Parse(page.StructuredDataJson)
            },
            Sections = sections
        });
    }

    /// <summary>Brand, theme tokens, contact, socials and the announcement bar in one call.</summary>
    [HttpGet("settings")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SiteSettingsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SiteSettingsResponse>> Settings(CancellationToken ct)
    {
        var settings = await _db.SiteSettings
            .AsNoTracking()
            .Where(s => s.IsPublic)
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        var branches = await _db.Branches
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.DisplayOrder)
            .Select(b => new BranchSummaryResponse
            {
                Id = b.Id, Name = b.Name, Slug = b.Slug, City = b.City,
                AddressLine1 = b.AddressLine1, AddressLine2 = b.AddressLine2, Pincode = b.Pincode,
                Phone = b.Phone, WhatsAppNumber = b.WhatsAppNumber, Email = b.Email,
                Latitude = b.Latitude, Longitude = b.Longitude, GoogleMapsUrl = b.GoogleMapsUrl,
                HeroImageUrl = b.HeroImageUrl, ShortPitch = b.ShortPitch,
                WeekdayHours = $"{b.WeekdayOpen:HH\\:mm} – {b.WeekdayClose:HH\\:mm}",
                WeekendHours = $"{b.WeekendOpen:HH\\:mm} – {b.WeekendClose:HH\\:mm}",
                OccupancyCapacity = b.OccupancyCapacity, DisplayOrder = b.DisplayOrder
            })
            .ToListAsync(ct);

        AnnouncementResponse? announcement = null;
        if (settings.TryGetValue("announcement.enabled", out var enabled) &&
            bool.TryParse(enabled, out var isOn) && isOn &&
            settings.TryGetValue("announcement.text", out var text) &&
            !string.IsNullOrWhiteSpace(text))
        {
            announcement = new AnnouncementResponse
            {
                Text = text,
                LinkLabel = settings.GetValueOrDefault("announcement.linkLabel"),
                LinkUrl = settings.GetValueOrDefault("announcement.linkUrl")
            };
        }

        return Ok(new SiteSettingsResponse
        {
            Values = settings,
            Branches = branches,
            Announcement = announcement
        });
    }

    // ------------------------------------------------------------------ admin writes

    /// <summary>Saves structured content to the section's draft slot, or publishes it outright.</summary>
    [HttpPut("sections/{id:int}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(CmsSectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CmsSectionResponse>> UpdateSection(
        int id, UpdateSectionRequest request, CancellationToken ct)
    {
        var section = await _db.CmsSections.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (section is null) return NotFound();

        var json = request.Content.ToJsonString();
        if (request.Publish)
        {
            section.ContentJson = json;
            section.DraftContentJson = null;
            section.State = PublishState.Published;
            section.PublishedAtUtc = DateTime.UtcNow;
            section.PublishedBy = User.Identity?.Name;
        }
        else
        {
            section.DraftContentJson = json;
        }

        if (request.IsVisible is bool visible) section.IsVisible = visible;
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("CMS section {Key} (#{Id}) {Action} by {User}",
            section.Key, section.Id, request.Publish ? "published" : "saved as draft", User.Identity?.Name);

        return Ok(ToResponse(section, preview: true));
    }

    /// <summary>Promotes the draft to live content.</summary>
    [HttpPost("sections/{id:int}/publish")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(typeof(CmsSectionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CmsSectionResponse>> PublishSection(int id, CancellationToken ct)
    {
        var section = await _db.CmsSections.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (section is null) return NotFound();

        if (section.DraftContentJson is not null)
        {
            section.ContentJson = section.DraftContentJson;
            section.DraftContentJson = null;
        }
        section.State = PublishState.Published;
        section.PublishedAtUtc = DateTime.UtcNow;
        section.PublishedBy = User.Identity?.Name;
        await _db.SaveChangesAsync(ct);

        return Ok(ToResponse(section, preview: true));
    }

    /// <summary>Drag-to-reorder: writes the new OrderIndex for every section on the page.</summary>
    [HttpPost("pages/{pageId:int}/reorder")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reorder(int pageId, ReorderSectionsRequest request, CancellationToken ct)
    {
        var sections = await _db.CmsSections.Where(s => s.CmsPageId == pageId).ToListAsync(ct);
        if (sections.Count == 0) return NotFound();

        var order = request.SectionIds
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);

        foreach (var section in sections)
            if (order.TryGetValue(section.Id, out var index))
                section.OrderIndex = index + 1;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Section-level show/hide toggle used by the admin page editor.</summary>
    [HttpPost("sections/{id:int}/visibility")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetVisibility(int id, [FromQuery] bool visible, CancellationToken ct)
    {
        var section = await _db.CmsSections.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (section is null) return NotFound();

        section.IsVisible = visible;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Updates one site setting (brand, theme token, WhatsApp number, SEO default).</summary>
    [HttpPut("settings/{key}")]
    [Authorize(Roles = RoleNames.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateSetting(string key, [FromBody] string value, CancellationToken ct)
    {
        var setting = await _db.SiteSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null) return NotFound();

        setting.Value = value;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static CmsSectionResponse ToResponse(CmsSection s, bool preview) => new()
    {
        Id = s.Id,
        Key = s.Key,
        Type = s.SectionType,
        TypeName = s.SectionType.ToString(),
        OrderIndex = s.OrderIndex,
        IsVisible = s.IsVisible,
        BranchId = s.BranchId,
        // In preview an unpublished draft wins; publicly only live content is ever served.
        Content = CmsSectionResponse.Parse(
            preview && s.DraftContentJson is not null ? s.DraftContentJson : s.ContentJson),
        Draft = preview && s.DraftContentJson is not null ? CmsSectionResponse.Parse(s.DraftContentJson) : null,
        State = s.State,
        PublishedAtUtc = s.PublishedAtUtc,
        AdminLabel = preview ? s.AdminLabel : null
    };
}
