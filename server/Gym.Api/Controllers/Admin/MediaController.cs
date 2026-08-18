using System.Text.Json;
using Gym.Api.Contracts;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers.Admin;

/// <summary>
/// The media library (Module 2A). Every upload is transcoded to WebP with a full set of width
/// renditions and a tiny blurred placeholder, so anything the owner picks in a section editor
/// arrives on the public site already optimised — the LCP budget is not something the person
/// writing copy should have to think about.
/// </summary>
[ApiController]
[Route("api/media")]
[Authorize(Roles = RoleNames.Admin)]
[Produces("application/json")]
public class MediaController : ControllerBase
{
    /// <summary>25 MB — comfortably above a 4K JPEG, well below a video the owner should not upload here.</summary>
    private const long MaxUploadBytes = 25 * 1024 * 1024;

    private static readonly string[] ImageTypes =
        { "image/jpeg", "image/png", "image/webp", "image/avif", "image/gif" };

    private readonly GymDbContext _db;
    private readonly IMediaStorage _storage;
    private readonly ILogger<MediaController> _log;

    public MediaController(GymDbContext db, IMediaStorage storage, ILogger<MediaController> log)
    {
        _db = db;
        _storage = storage;
        _log = log;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<MediaAssetResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<MediaAssetResponse>>> List(
        [FromQuery] string? q, [FromQuery] string? folder, [FromQuery] MediaKind? kind,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 40, CancellationToken ct = default)
    {
        var query = _db.MediaAssets.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(m =>
                m.FileName.Contains(term) || m.AltText.Contains(term) ||
                (m.Tags != null && m.Tags.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(folder)) query = query.Where(m => m.Folder == folder);
        if (kind is not null) query = query.Where(m => m.Kind == kind);

        var total = await query.CountAsync(ct);
        var size = Math.Clamp(pageSize, 1, 120);
        var items = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .Skip((Math.Max(1, page) - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return Ok(new PagedResult<MediaAssetResponse>
        {
            Items = items.Select(Describe).ToList(),
            Total = total,
            Page = Math.Max(1, page),
            PageSize = size
        });
    }

    [HttpGet("folders")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> Folders(CancellationToken ct) =>
        Ok(await _db.MediaAssets
            .AsNoTracking()
            .Where(m => m.Folder != null && m.Folder != "")
            .Select(m => m.Folder!)
            .Distinct()
            .OrderBy(f => f)
            .ToListAsync(ct));

    [HttpPost]
    [RequestSizeLimit(MaxUploadBytes)]
    [ProducesResponseType(typeof(MediaAssetResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MediaAssetResponse>> Upload(
        // No [FromForm] on the file itself: it binds from the multipart body anyway, and
        // Swashbuckle refuses to describe an IFormFile that carries the attribute.
        IFormFile file,
        [FromForm] string? altText,
        [FromForm] string? folder,
        [FromForm] string? caption,
        [FromForm] string? credit,
        [FromForm] string? tags,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(nameof(file), "Choose a file to upload.");
            return ValidationProblem(ModelState);
        }
        if (file.Length > MaxUploadBytes)
        {
            ModelState.AddModelError(nameof(file), "That file is over the 25 MB limit.");
            return ValidationProblem(ModelState);
        }

        var contentType = file.ContentType?.ToLowerInvariant() ?? "application/octet-stream";
        var isImage = ImageTypes.Contains(contentType);

        // Alt text is not optional on an image: a gallery of unlabelled photographs fails
        // WCAG the moment the owner drops one into a section.
        if (isImage && string.IsNullOrWhiteSpace(altText))
        {
            ModelState.AddModelError(nameof(altText), "Describe the image for screen readers.");
            return ValidationProblem(ModelState);
        }

        await using var stream = file.OpenReadStream();
        StoredMedia stored;
        try
        {
            stored = isImage
                ? await _storage.SaveImageAsync(stream, file.FileName, folder, ct)
                : await _storage.SaveFileAsync(stream, file.FileName, contentType, folder, ct);
        }
        catch (Exception ex) when (isImage)
        {
            _log.LogWarning(ex, "Could not decode {FileName} as an image", file.FileName);
            ModelState.AddModelError(nameof(file), "That file could not be read as an image.");
            return ValidationProblem(ModelState);
        }

        var asset = new MediaAsset
        {
            FileName = Path.GetFileName(stored.OriginalUrl),
            OriginalUrl = stored.OriginalUrl,
            Kind = isImage ? MediaKind.Image : contentType.StartsWith("video/") ? MediaKind.Video : MediaKind.Document,
            ContentType = stored.ContentType,
            SizeBytes = stored.SizeBytes,
            Width = stored.Width,
            Height = stored.Height,
            VariantsJson = JsonSerializer.Serialize(stored.Variants),
            BlurDataUrl = stored.BlurDataUrl,
            AltText = altText?.Trim() ?? string.Empty,
            Caption = caption,
            Credit = credit,
            Folder = string.IsNullOrWhiteSpace(folder) ? null : folder.Trim(),
            Tags = tags,
            UploadedBy = User.Identity?.Name
        };

        _db.MediaAssets.Add(asset);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Media {File} uploaded ({Variants} WebP variants) by {User}",
            asset.FileName, stored.Variants.Count, User.Identity?.Name);

        return CreatedAtAction(nameof(List), new { id = asset.Id }, Describe(asset));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(MediaAssetResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MediaAssetResponse>> Update(
        int id, UpdateMediaRequest request, CancellationToken ct)
    {
        var asset = await _db.MediaAssets.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (asset is null) return NotFound();

        asset.AltText = request.AltText.Trim();
        asset.Caption = request.Caption;
        asset.Credit = request.Credit;
        asset.Folder = string.IsNullOrWhiteSpace(request.Folder) ? null : request.Folder.Trim();
        asset.Tags = request.Tags;
        asset.UpdatedBy = User.Identity?.Name;

        await _db.SaveChangesAsync(ct);
        return Ok(Describe(asset));
    }

    /// <summary>
    /// Removes the row and every rendition on disk. Refuses while the URL is still referenced
    /// by a section, a trainer portrait or a class cover — a deleted image is a broken page.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, [FromQuery] bool force = false, CancellationToken ct = default)
    {
        var asset = await _db.MediaAssets.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (asset is null) return NotFound();

        if (!force)
        {
            var url = asset.OriginalUrl;
            var usedBy = new List<string>();

            if (await _db.CmsSections.AnyAsync(s =>
                    s.ContentJson.Contains(url) || (s.DraftContentJson != null && s.DraftContentJson.Contains(url)), ct))
                usedBy.Add("a page section");
            if (await _db.Trainers.AnyAsync(t => t.PortraitUrl == url, ct)) usedBy.Add("a coach portrait");
            if (await _db.ClassFormats.AnyAsync(f => f.CoverImageUrl == url, ct)) usedBy.Add("a class cover");
            if (await _db.BlogPosts.AnyAsync(p => p.CoverImageUrl == url || p.OgImageUrl == url, ct)) usedBy.Add("a journal post");
            if (await _db.Branches.AnyAsync(b => b.HeroImageUrl == url, ct)) usedBy.Add("a branch hero");

            if (usedBy.Count > 0)
                return Conflict(new ProblemDetails
                {
                    Title = "Still in use",
                    Detail = $"This file is used by {string.Join(", ", usedBy)}. " +
                             "Replace it there first, or delete with force=true.",
                    Status = StatusCodes.Status409Conflict
                });
        }

        var variants = ParseVariants(asset.VariantsJson);
        foreach (var url in variants.Values.Append(asset.OriginalUrl))
        {
            try { await _storage.DeleteAsync(url, ct); }
            catch (Exception ex) { _log.LogWarning(ex, "Could not delete {Url} from disk", url); }
        }

        _db.MediaAssets.Remove(asset);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static MediaAssetResponse Describe(MediaAsset m) => new()
    {
        Id = m.Id,
        FileName = m.FileName,
        Url = m.OriginalUrl,
        Kind = m.Kind,
        ContentType = m.ContentType,
        SizeBytes = m.SizeBytes,
        Width = m.Width,
        Height = m.Height,
        Variants = ParseVariants(m.VariantsJson),
        BlurDataUrl = m.BlurDataUrl,
        AltText = m.AltText,
        Caption = m.Caption,
        Credit = m.Credit,
        Folder = m.Folder,
        Tags = string.IsNullOrWhiteSpace(m.Tags)
            ? Array.Empty<string>()
            : m.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        UploadedBy = m.UploadedBy,
        CreatedAtUtc = m.CreatedAtUtc
    };

    private static IReadOnlyDictionary<string, string> ParseVariants(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }
}

/// <summary>Every admin list returns this envelope — the tables all paginate the same way.</summary>
public record PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int Total { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public int PageCount => PageSize == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}
