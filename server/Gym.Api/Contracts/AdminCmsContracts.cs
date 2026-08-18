using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using Gym.Core.Enums;

namespace Gym.Api.Contracts;

/* ============================================================================
   Module 2A — the CMS the owner runs the public site from.

   The admin reads are deliberately richer than the public ones: they carry draft
   content, hidden sections, unpublished pages and the admin label, none of which
   a visitor may ever see.
   ============================================================================ */

public record AdminPageListItem
{
    public required int Id { get; init; }
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public required PublishState State { get; init; }
    public required bool IsSystemPage { get; init; }
    public required int DisplayOrder { get; init; }
    public required int SectionCount { get; init; }
    public required int HiddenSectionCount { get; init; }
    public required int DraftSectionCount { get; init; }
    public DateTime? PublishedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

public record AdminPageResponse
{
    public required int Id { get; init; }
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required CmsSeoResponse Seo { get; init; }
    public required PublishState State { get; init; }
    public required bool IsSystemPage { get; init; }
    public required int DisplayOrder { get; init; }
    public required IReadOnlyList<AdminSectionResponse> Sections { get; init; }
}

public record AdminSectionResponse
{
    public required int Id { get; init; }
    public required string Key { get; init; }
    public required CmsSectionType Type { get; init; }
    public required string TypeName { get; init; }
    public required string AdminLabel { get; init; }
    public required int OrderIndex { get; init; }
    public required bool IsVisible { get; init; }
    public int? BranchId { get; init; }
    public string? BranchName { get; init; }
    /// <summary>Live content served to the public site.</summary>
    public required JsonNode Content { get; init; }
    /// <summary>Unsaved-to-live edits. Null when the section has no pending draft.</summary>
    public JsonNode? Draft { get; init; }
    public required bool HasDraft { get; init; }
    public required PublishState State { get; init; }
    public DateTime? PublishedAtUtc { get; init; }
    public string? PublishedBy { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

public record UpsertPageRequest
{
    [Required, MaxLength(120)] public string Slug { get; init; } = string.Empty;
    [Required, MaxLength(200)] public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    [Required] public string SeoTitle { get; init; } = string.Empty;
    [Required] public string SeoDescription { get; init; } = string.Empty;
    public string? SeoKeywords { get; init; }
    public string? OgImageUrl { get; init; }
    public string? CanonicalUrl { get; init; }
    public bool NoIndex { get; init; }
    public JsonNode? StructuredData { get; init; }
    public PublishState State { get; init; } = PublishState.Draft;
    public int DisplayOrder { get; init; }
}

public record CreateSectionRequest
{
    [Required] public CmsSectionType SectionType { get; init; }
    [Required, MaxLength(80)] public string Key { get; init; } = string.Empty;
    [Required, MaxLength(120)] public string AdminLabel { get; init; } = string.Empty;
    /// <summary>Starting content. The client sends the Zod default object for the type.</summary>
    public JsonNode? Content { get; init; }
    public int? BranchId { get; init; }
    /// <summary>Insert position; appended when omitted.</summary>
    public int? OrderIndex { get; init; }
}

public record AdminSettingResponse
{
    public required int Id { get; init; }
    public required string Key { get; init; }
    public required string Value { get; init; }
    public required string Group { get; init; }
    public required string Label { get; init; }
    public string? HelpText { get; init; }
    public required string ValueType { get; init; }
    public required bool IsPublic { get; init; }
    public required int DisplayOrder { get; init; }
}

public record UpdateSettingsRequest
{
    /// <summary>Key → new value. Unknown keys are reported rather than silently created.</summary>
    [Required] public Dictionary<string, string> Values { get; init; } = new();
}

public record CreateSettingRequest
{
    [Required, MaxLength(120)] public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    [Required] public string Group { get; init; } = "General";
    [Required] public string Label { get; init; } = string.Empty;
    public string? HelpText { get; init; }
    public string ValueType { get; init; } = "text";
    public bool IsPublic { get; init; } = true;
    public int DisplayOrder { get; init; }
}

/* ---------------------------------------------------------------- media library */

public record MediaAssetResponse
{
    public required int Id { get; init; }
    public required string FileName { get; init; }
    public required string Url { get; init; }
    public required MediaKind Kind { get; init; }
    public required string ContentType { get; init; }
    public required long SizeBytes { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    /// <summary>Width → WebP URL, e.g. {"960":"/media/uploads/floor-960.webp"}.</summary>
    public required IReadOnlyDictionary<string, string> Variants { get; init; }
    public string? BlurDataUrl { get; init; }
    public required string AltText { get; init; }
    public string? Caption { get; init; }
    public string? Credit { get; init; }
    public string? Folder { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public string? UploadedBy { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}

public record UpdateMediaRequest
{
    [Required] public string AltText { get; init; } = string.Empty;
    public string? Caption { get; init; }
    public string? Credit { get; init; }
    public string? Folder { get; init; }
    public string? Tags { get; init; }
}

/* ---------------------------------------------------------------- collections */

public record TestimonialRequest
{
    [Required, MaxLength(160)] public string AuthorName { get; init; } = string.Empty;
    public string? AuthorRole { get; init; }
    public string? AuthorPhotoUrl { get; init; }
    [Required] public string Quote { get; init; } = string.Empty;
    [Range(1, 5)] public int Rating { get; init; } = 5;
    public int? BranchId { get; init; }
    public string? Program { get; init; }
    public string? GoogleReviewUrl { get; init; }
    public bool IsFeatured { get; init; }
    public bool IsVisible { get; init; } = true;
    public int DisplayOrder { get; init; }
}

public record TransformationRequest
{
    [Required, MaxLength(160)] public string MemberDisplayName { get; init; } = string.Empty;
    public int? MemberId { get; init; }
    [Required] public string BeforeImageUrl { get; init; } = string.Empty;
    [Required] public string AfterImageUrl { get; init; } = string.Empty;
    [Range(1, 520)] public int DurationWeeks { get; init; } = 12;
    [Required] public string Program { get; init; } = string.Empty;
    public string? TrainerName { get; init; }
    public decimal? WeightBeforeKg { get; init; }
    public decimal? WeightAfterKg { get; init; }
    public string? Story { get; init; }
    public int? BranchId { get; init; }
    /// <summary>Hard gate — the public gallery never renders a row without this.</summary>
    public bool ConsentGiven { get; init; }
    public bool IsVisible { get; init; } = true;
    public int DisplayOrder { get; init; }
}

public record FaqRequest
{
    [Required] public string Question { get; init; } = string.Empty;
    [Required] public string Answer { get; init; } = string.Empty;
    [Required] public string Category { get; init; } = "General";
    public int? BranchId { get; init; }
    public bool IsVisible { get; init; } = true;
    public int DisplayOrder { get; init; }
}

public record BlogPostRequest
{
    [Required, MaxLength(160)] public string Slug { get; init; } = string.Empty;
    [Required, MaxLength(220)] public string Title { get; init; } = string.Empty;
    [Required] public string Excerpt { get; init; } = string.Empty;
    /// <summary>Structured blocks, never raw HTML — the typography stays on-system.</summary>
    public JsonNode? Body { get; init; }
    public string? CoverImageUrl { get; init; }
    [Required] public string AuthorName { get; init; } = string.Empty;
    public string? AuthorRole { get; init; }
    public string? Tags { get; init; }
    [Range(1, 90)] public int ReadMinutes { get; init; } = 5;
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
    public string? OgImageUrl { get; init; }
    public PublishState State { get; init; } = PublishState.Draft;
    public bool IsFeatured { get; init; }
}
