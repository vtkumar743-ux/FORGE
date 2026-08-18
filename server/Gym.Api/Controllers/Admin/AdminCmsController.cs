using System.Text.Json.Nodes;
using Gym.Api.Contracts;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers.Admin;

/// <summary>
/// Everything the owner needs to run the public site without a developer: pages, their SEO,
/// the sections on them (create, order, hide, draft, publish) and the site settings.
///
/// The public read path stays on <c>/api/cms</c>; this controller is the write side and is
/// Admin-only end to end. Content is stored as structured JSON per section type — the admin
/// UI renders a form from the same Zod shape the renderer validates against, so an edit can
/// never produce content the public site cannot draw.
/// </summary>
[ApiController]
[Route("api/admin/cms")]
[Authorize(Roles = RoleNames.Admin)]
[Produces("application/json")]
public class AdminCmsController : ControllerBase
{
    private readonly GymDbContext _db;
    private readonly ILogger<AdminCmsController> _log;

    public AdminCmsController(GymDbContext db, ILogger<AdminCmsController> log)
    {
        _db = db;
        _log = log;
    }

    // ------------------------------------------------------------------ pages

    [HttpGet("pages")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminPageListItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminPageListItem>>> Pages(CancellationToken ct)
    {
        var pages = await _db.CmsPages
            .AsNoTracking()
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Slug)
            .Select(p => new AdminPageListItem
            {
                Id = p.Id,
                Slug = p.Slug,
                Title = p.Title,
                State = p.State,
                IsSystemPage = p.IsSystemPage,
                DisplayOrder = p.DisplayOrder,
                SectionCount = p.Sections.Count,
                HiddenSectionCount = p.Sections.Count(s => !s.IsVisible),
                DraftSectionCount = p.Sections.Count(s => s.DraftContentJson != null),
                PublishedAtUtc = p.PublishedAtUtc,
                UpdatedAtUtc = p.UpdatedAtUtc
            })
            .ToListAsync(ct);

        return Ok(pages);
    }

    [HttpGet("pages/{id:int}")]
    [ProducesResponseType(typeof(AdminPageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminPageResponse>> Page(int id, CancellationToken ct)
    {
        var page = await _db.CmsPages
            .AsNoTracking()
            .Include(p => p.Sections).ThenInclude(s => s.Branch)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        return page is null ? NotFound() : Ok(Describe(page));
    }

    [HttpPost("pages")]
    [ProducesResponseType(typeof(AdminPageResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<AdminPageResponse>> CreatePage(UpsertPageRequest request, CancellationToken ct)
    {
        var slug = Slugify(request.Slug);
        if (await _db.CmsPages.AnyAsync(p => p.Slug == slug, ct))
        {
            ModelState.AddModelError(nameof(request.Slug), $"A page already lives at /{slug}.");
            return ValidationProblem(ModelState);
        }

        var page = new CmsPage { Slug = slug, IsSystemPage = false };
        Apply(page, request);
        _db.CmsPages.Add(page);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("CMS page /{Slug} created by {User}", slug, User.Identity?.Name);
        return CreatedAtAction(nameof(Page), new { id = page.Id }, Describe(page));
    }

    [HttpPut("pages/{id:int}")]
    [ProducesResponseType(typeof(AdminPageResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminPageResponse>> UpdatePage(
        int id, UpsertPageRequest request, CancellationToken ct)
    {
        var page = await _db.CmsPages.Include(p => p.Sections).ThenInclude(s => s.Branch)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (page is null) return NotFound();

        // A system page's slug is wired into the router; renaming it would 404 a live route.
        if (!page.IsSystemPage) page.Slug = Slugify(request.Slug);
        Apply(page, request);
        page.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync(ct);

        return Ok(Describe(page));
    }

    [HttpPost("pages/{id:int}/publish")]
    [ProducesResponseType(typeof(AdminPageResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminPageResponse>> PublishPage(int id, CancellationToken ct)
    {
        var page = await _db.CmsPages.Include(p => p.Sections).ThenInclude(s => s.Branch)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (page is null) return NotFound();

        var now = DateTime.UtcNow;
        page.State = PublishState.Published;
        page.PublishedAtUtc = now;

        // Publishing a page promotes every pending section draft with it — that is what the
        // owner means by "publish", and leaving drafts behind is how stale copy ships.
        foreach (var section in page.Sections.Where(s => s.DraftContentJson is not null))
        {
            section.ContentJson = section.DraftContentJson!;
            section.DraftContentJson = null;
            section.State = PublishState.Published;
            section.PublishedAtUtc = now;
            section.PublishedBy = User.Identity?.Name;
        }

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("CMS page /{Slug} published by {User}", page.Slug, User.Identity?.Name);
        return Ok(Describe(page));
    }

    [HttpPost("pages/{id:int}/unpublish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UnpublishPage(int id, CancellationToken ct)
    {
        var page = await _db.CmsPages.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (page is null) return NotFound();
        if (page.IsSystemPage) return Problem("A system page cannot be taken offline.", statusCode: 400);

        page.State = PublishState.Draft;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("pages/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeletePage(int id, CancellationToken ct)
    {
        var page = await _db.CmsPages.Include(p => p.Sections).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (page is null) return NotFound();
        if (page.IsSystemPage) return Problem("System pages can be edited but not deleted.", statusCode: 400);

        _db.CmsSections.RemoveRange(page.Sections);
        _db.CmsPages.Remove(page);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ------------------------------------------------------------------ sections

    [HttpPost("pages/{pageId:int}/sections")]
    [ProducesResponseType(typeof(AdminSectionResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<AdminSectionResponse>> CreateSection(
        int pageId, CreateSectionRequest request, CancellationToken ct)
    {
        var page = await _db.CmsPages.Include(p => p.Sections).FirstOrDefaultAsync(p => p.Id == pageId, ct);
        if (page is null) return NotFound();

        var key = Slugify(request.Key);
        if (page.Sections.Any(s => s.Key == key))
        {
            ModelState.AddModelError(nameof(request.Key), $"This page already has a section keyed \"{key}\".");
            return ValidationProblem(ModelState);
        }

        var section = new CmsSection
        {
            CmsPageId = pageId,
            SectionType = request.SectionType,
            Key = key,
            AdminLabel = request.AdminLabel,
            BranchId = request.BranchId,
            OrderIndex = request.OrderIndex ?? (page.Sections.Count == 0 ? 1 : page.Sections.Max(s => s.OrderIndex) + 1),
            // New sections start hidden and unpublished: the owner fills them in, previews,
            // then reveals. Nothing half-written is ever live for a second.
            IsVisible = false,
            State = PublishState.Draft,
            ContentJson = (request.Content ?? new JsonObject()).ToJsonString(),
            CreatedBy = User.Identity?.Name
        };

        _db.CmsSections.Add(section);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("CMS section {Key} ({Type}) added to /{Slug}", key, request.SectionType, page.Slug);
        return CreatedAtAction(nameof(Page), new { id = pageId }, Describe(section));
    }

    /// <summary>Copies a section, content and all, so a second variant starts from a working one.</summary>
    [HttpPost("sections/{id:int}/duplicate")]
    [ProducesResponseType(typeof(AdminSectionResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<AdminSectionResponse>> DuplicateSection(int id, CancellationToken ct)
    {
        var source = await _db.CmsSections.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (source is null) return NotFound();

        var siblings = await _db.CmsSections.Where(s => s.CmsPageId == source.CmsPageId).Select(s => s.Key).ToListAsync(ct);
        var key = source.Key;
        var suffix = 2;
        while (siblings.Contains(key)) key = $"{source.Key}-{suffix++}";

        var copy = new CmsSection
        {
            CmsPageId = source.CmsPageId,
            SectionType = source.SectionType,
            Key = key,
            AdminLabel = $"{source.AdminLabel} (copy)",
            OrderIndex = source.OrderIndex + 1,
            IsVisible = false,
            State = PublishState.Draft,
            ContentJson = source.DraftContentJson ?? source.ContentJson,
            BranchId = source.BranchId,
            CreatedBy = User.Identity?.Name
        };

        _db.CmsSections.Add(copy);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Page), new { id = source.CmsPageId }, Describe(copy));
    }

    /// <summary>Throws away a pending draft and reverts the editor to what is live.</summary>
    [HttpPost("sections/{id:int}/discard-draft")]
    [ProducesResponseType(typeof(AdminSectionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminSectionResponse>> DiscardDraft(int id, CancellationToken ct)
    {
        var section = await _db.CmsSections.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (section is null) return NotFound();

        section.DraftContentJson = null;
        await _db.SaveChangesAsync(ct);
        return Ok(Describe(section));
    }

    [HttpPatch("sections/{id:int}/meta")]
    [ProducesResponseType(typeof(AdminSectionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminSectionResponse>> UpdateSectionMeta(
        int id, [FromBody] UpdateSectionMetaRequest request, CancellationToken ct)
    {
        var section = await _db.CmsSections.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (section is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(request.AdminLabel)) section.AdminLabel = request.AdminLabel;
        if (request.IsVisible is bool visible) section.IsVisible = visible;
        if (request.BranchId is not null) section.BranchId = request.BranchId == 0 ? null : request.BranchId;

        await _db.SaveChangesAsync(ct);
        return Ok(Describe(section));
    }

    [HttpDelete("sections/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSection(int id, CancellationToken ct)
    {
        var section = await _db.CmsSections.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (section is null) return NotFound();

        _db.CmsSections.Remove(section);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("CMS section {Key} deleted by {User}", section.Key, User.Identity?.Name);
        return NoContent();
    }

    // ------------------------------------------------------------------ settings

    [HttpGet("settings")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminSettingResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminSettingResponse>>> Settings(CancellationToken ct)
    {
        var settings = await _db.SiteSettings
            .AsNoTracking()
            .OrderBy(s => s.Group).ThenBy(s => s.DisplayOrder).ThenBy(s => s.Key)
            .Select(s => new AdminSettingResponse
            {
                Id = s.Id, Key = s.Key, Value = s.Value, Group = s.Group, Label = s.Label,
                HelpText = s.HelpText, ValueType = s.ValueType, IsPublic = s.IsPublic,
                DisplayOrder = s.DisplayOrder
            })
            .ToListAsync(ct);

        return Ok(settings);
    }

    [HttpPut("settings")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminSettingResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminSettingResponse>>> UpdateSettings(
        UpdateSettingsRequest request, CancellationToken ct)
    {
        var keys = request.Values.Keys.ToList();
        var rows = await _db.SiteSettings.Where(s => keys.Contains(s.Key)).ToListAsync(ct);

        var unknown = keys.Except(rows.Select(r => r.Key)).ToList();
        if (unknown.Count > 0)
        {
            ModelState.AddModelError(nameof(request.Values),
                $"Unknown setting key(s): {string.Join(", ", unknown)}.");
            return ValidationProblem(ModelState);
        }

        foreach (var row in rows)
        {
            row.Value = request.Values[row.Key];
            row.UpdatedBy = User.Identity?.Name;
        }

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("{Count} site setting(s) updated by {User}", rows.Count, User.Identity?.Name);
        return await Settings(ct);
    }

    [HttpPost("settings")]
    [ProducesResponseType(typeof(AdminSettingResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<AdminSettingResponse>> CreateSetting(
        CreateSettingRequest request, CancellationToken ct)
    {
        if (await _db.SiteSettings.AnyAsync(s => s.Key == request.Key, ct))
        {
            ModelState.AddModelError(nameof(request.Key), "That key already exists.");
            return ValidationProblem(ModelState);
        }

        var setting = new SiteSetting
        {
            Key = request.Key.Trim(),
            Value = request.Value,
            Group = request.Group,
            Label = request.Label,
            HelpText = request.HelpText,
            ValueType = request.ValueType,
            IsPublic = request.IsPublic,
            DisplayOrder = request.DisplayOrder,
            CreatedBy = User.Identity?.Name
        };

        _db.SiteSettings.Add(setting);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Settings), new AdminSettingResponse
        {
            Id = setting.Id, Key = setting.Key, Value = setting.Value, Group = setting.Group,
            Label = setting.Label, HelpText = setting.HelpText, ValueType = setting.ValueType,
            IsPublic = setting.IsPublic, DisplayOrder = setting.DisplayOrder
        });
    }

    // ------------------------------------------------------------------ helpers

    private static void Apply(CmsPage page, UpsertPageRequest request)
    {
        page.Title = request.Title;
        page.Description = request.Description;
        page.SeoTitle = request.SeoTitle;
        page.SeoDescription = request.SeoDescription;
        page.SeoKeywords = request.SeoKeywords;
        page.OgImageUrl = request.OgImageUrl;
        page.CanonicalUrl = request.CanonicalUrl;
        page.NoIndex = request.NoIndex;
        page.StructuredDataJson = request.StructuredData?.ToJsonString();
        page.DisplayOrder = request.DisplayOrder;

        if (request.State == PublishState.Published && page.State != PublishState.Published)
            page.PublishedAtUtc = DateTime.UtcNow;
        page.State = request.State;
    }

    private static AdminPageResponse Describe(CmsPage page) => new()
    {
        Id = page.Id,
        Slug = page.Slug,
        Title = page.Title,
        Description = page.Description,
        Seo = new CmsSeoResponse
        {
            Title = page.SeoTitle,
            Description = page.SeoDescription,
            Keywords = page.SeoKeywords,
            OgImageUrl = page.OgImageUrl,
            CanonicalUrl = page.CanonicalUrl,
            NoIndex = page.NoIndex,
            StructuredData = page.StructuredDataJson is null ? null : CmsSectionResponse.Parse(page.StructuredDataJson)
        },
        State = page.State,
        IsSystemPage = page.IsSystemPage,
        DisplayOrder = page.DisplayOrder,
        Sections = page.Sections.OrderBy(s => s.OrderIndex).Select(Describe).ToList()
    };

    private static AdminSectionResponse Describe(CmsSection s) => new()
    {
        Id = s.Id,
        Key = s.Key,
        Type = s.SectionType,
        TypeName = s.SectionType.ToString(),
        AdminLabel = string.IsNullOrWhiteSpace(s.AdminLabel) ? s.SectionType.ToString() : s.AdminLabel,
        OrderIndex = s.OrderIndex,
        IsVisible = s.IsVisible,
        BranchId = s.BranchId,
        BranchName = s.Branch?.Name,
        Content = CmsSectionResponse.Parse(s.ContentJson),
        Draft = s.DraftContentJson is null ? null : CmsSectionResponse.Parse(s.DraftContentJson),
        HasDraft = s.DraftContentJson is not null,
        State = s.State,
        PublishedAtUtc = s.PublishedAtUtc,
        PublishedBy = s.PublishedBy,
        UpdatedAtUtc = s.UpdatedAtUtc
    };

    internal static string Slugify(string value)
    {
        var cleaned = new string(value.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        while (cleaned.Contains("--")) cleaned = cleaned.Replace("--", "-");
        return cleaned.Trim('-');
    }
}

public record UpdateSectionMetaRequest
{
    public string? AdminLabel { get; init; }
    public bool? IsVisible { get; init; }
    /// <summary>0 clears the branch scope back to "shown on every branch page".</summary>
    public int? BranchId { get; init; }
}
