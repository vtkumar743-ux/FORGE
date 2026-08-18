using Gym.Core.Enums;

namespace Gym.Core.Entities;

/// <summary>
/// One row per public route. SEO lives here so every page's meta is owner-editable
/// (Module 1.12) rather than compiled into the bundle.
/// </summary>
public class CmsPage : BaseEntity
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string SeoTitle { get; set; } = string.Empty;
    public string SeoDescription { get; set; } = string.Empty;
    public string? SeoKeywords { get; set; }
    public string? OgImageUrl { get; set; }
    public string? CanonicalUrl { get; set; }
    public bool NoIndex { get; set; }
    /// <summary>JSON-LD blob (LocalBusiness / HealthClub) injected into the page head.</summary>
    public string? StructuredDataJson { get; set; }

    public PublishState State { get; set; } = PublishState.Published;
    public DateTime? PublishedAtUtc { get; set; }
    /// <summary>System pages cannot be deleted from the admin UI, only edited.</summary>
    public bool IsSystemPage { get; set; }
    public int DisplayOrder { get; set; }

    public ICollection<CmsSection> Sections { get; set; } = new List<CmsSection>();
}

/// <summary>
/// The heart of the CMS: every public-site section is one row, rendered from
/// <see cref="ContentJson"/> which is typed per <see cref="SectionType"/> (Zod on the
/// client, JSON schema on the API). The owner edits structured fields, never HTML,
/// so the design system survives any amount of editing.
/// </summary>
public class CmsSection : BaseEntity
{
    public int CmsPageId { get; set; }
    public CmsPage CmsPage { get; set; } = null!;

    public CmsSectionType SectionType { get; set; }
    /// <summary>Stable handle used by the renderer and by deep links (#amenities).</summary>
    public string Key { get; set; } = string.Empty;
    public string AdminLabel { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public bool IsVisible { get; set; } = true;

    /// <summary>Live content served to the public site.</summary>
    public string ContentJson { get; set; } = "{}";
    /// <summary>Unpublished edits — previewable by an admin before going live.</summary>
    public string? DraftContentJson { get; set; }
    public PublishState State { get; set; } = PublishState.Published;
    public DateTime? PublishedAtUtc { get; set; }
    public string? PublishedBy { get; set; }
    /// <summary>Optional per-branch variant of an otherwise shared section.</summary>
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
}

public class MediaAsset : BaseEntity
{
    public string FileName { get; set; } = string.Empty;
    public string OriginalUrl { get; set; } = string.Empty;
    public MediaKind Kind { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }

    /// <summary>JSON map of WebP renditions: {"480":"…-480.webp","1920":"…-1920.webp"}.</summary>
    public string VariantsJson { get; set; } = "{}";
    public string? PosterUrl { get; set; }
    /// <summary>Tiny blurred placeholder so media never pops in as a grey box.</summary>
    public string? BlurDataUrl { get; set; }

    public string AltText { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public string? Credit { get; set; }
    public string? SourceUrl { get; set; }
    public string? Folder { get; set; }
    public string? Tags { get; set; }
    public string? UploadedBy { get; set; }
}

public class Testimonial : BaseEntity
{
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorRole { get; set; }
    public string? AuthorPhotoUrl { get; set; }
    public string Quote { get; set; } = string.Empty;
    public int Rating { get; set; } = 5;
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public string? Program { get; set; }
    public string? GoogleReviewUrl { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsVisible { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public class Transformation : BaseEntity
{
    public string MemberDisplayName { get; set; } = string.Empty;
    public int? MemberId { get; set; }
    public Member? Member { get; set; }
    public string BeforeImageUrl { get; set; } = string.Empty;
    public string AfterImageUrl { get; set; } = string.Empty;
    public int DurationWeeks { get; set; }
    public string Program { get; set; } = string.Empty;
    public string? TrainerName { get; set; }
    public decimal? WeightBeforeKg { get; set; }
    public decimal? WeightAfterKg { get; set; }
    public string? Story { get; set; }
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
    /// <summary>Hard gate — nothing is published without written member consent.</summary>
    public bool ConsentGiven { get; set; }
    public DateTime? ConsentAtUtc { get; set; }
    public bool IsVisible { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public class BlogPost : BaseEntity
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    /// <summary>Structured block JSON, not raw HTML — keeps typography on-system.</summary>
    public string BodyJson { get; set; } = "[]";
    public string? CoverImageUrl { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorRole { get; set; }
    public string? Tags { get; set; }
    public int ReadMinutes { get; set; }

    public string SeoTitle { get; set; } = string.Empty;
    public string SeoDescription { get; set; } = string.Empty;
    public string? OgImageUrl { get; set; }

    public PublishState State { get; set; } = PublishState.Draft;
    public DateTime? PublishedAtUtc { get; set; }
    public bool IsFeatured { get; set; }
    public int ViewCount { get; set; }
}

public class FaqItem : BaseEntity
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public bool IsVisible { get; set; } = true;
    public int DisplayOrder { get; set; }
}

/// <summary>Key-value site config: brand name, logo, theme tokens, WhatsApp, socials, SEO defaults.</summary>
public class SiteSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Group { get; set; } = "General";
    public string Label { get; set; } = string.Empty;
    public string? HelpText { get; set; }
    /// <summary>text | textarea | color | image | url | boolean | number — drives the admin form control.</summary>
    public string ValueType { get; set; } = "text";
    public bool IsPublic { get; set; } = true;
    public int DisplayOrder { get; set; }
}
