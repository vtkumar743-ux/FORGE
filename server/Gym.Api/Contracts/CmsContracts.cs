using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gym.Core.Enums;

namespace Gym.Api.Contracts;

/// <summary>A page plus its ordered, visible sections — one request renders a whole route.</summary>
public record CmsPageResponse
{
    public required int Id { get; init; }
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public required CmsSeoResponse Seo { get; init; }
    public required IReadOnlyList<CmsSectionResponse> Sections { get; init; }
}

public record CmsSeoResponse
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public string? Keywords { get; init; }
    public string? OgImageUrl { get; init; }
    public string? CanonicalUrl { get; init; }
    public bool NoIndex { get; init; }
    /// <summary>Parsed JSON-LD so the client can inject it without re-parsing a string.</summary>
    public JsonNode? StructuredData { get; init; }
}

public record CmsSectionResponse
{
    public required int Id { get; init; }
    public required string Key { get; init; }
    public required CmsSectionType Type { get; init; }
    public required string TypeName { get; init; }
    public required int OrderIndex { get; init; }
    public required bool IsVisible { get; init; }
    public int? BranchId { get; init; }
    /// <summary>Typed per section type; validated with Zod on the client.</summary>
    public required JsonNode Content { get; init; }
    /// <summary>Present only on admin/preview reads.</summary>
    public JsonNode? Draft { get; init; }
    public PublishState State { get; init; }
    public DateTime? PublishedAtUtc { get; init; }
    public string? AdminLabel { get; init; }

    public static JsonNode Parse(string json)
    {
        try
        {
            return JsonNode.Parse(json) ?? new JsonObject();
        }
        catch (JsonException)
        {
            // A section with malformed JSON must not take the whole page down.
            return new JsonObject { ["_error"] = "Section content is not valid JSON." };
        }
    }
}

public record UpdateSectionRequest
{
    /// <summary>Structured content for this section type. Saved to the draft slot.</summary>
    [Required]
    public JsonNode Content { get; init; } = new JsonObject();
    public bool? IsVisible { get; init; }
    /// <summary>True to publish immediately instead of leaving it as a draft.</summary>
    public bool Publish { get; init; }
}

public record ReorderSectionsRequest
{
    /// <summary>Section ids in their new visual order.</summary>
    [Required, MinLength(1)]
    public int[] SectionIds { get; init; } = Array.Empty<int>();
}

public record SiteSettingsResponse
{
    public required IReadOnlyDictionary<string, string> Values { get; init; }
    public required IReadOnlyList<BranchSummaryResponse> Branches { get; init; }
    public AnnouncementResponse? Announcement { get; init; }
}

public record AnnouncementResponse
{
    public required string Text { get; init; }
    public string? LinkLabel { get; init; }
    public string? LinkUrl { get; init; }
}

public record BranchSummaryResponse
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required string City { get; init; }
    public required string AddressLine1 { get; init; }
    public string? AddressLine2 { get; init; }
    public required string Pincode { get; init; }
    public required string Phone { get; init; }
    public string? WhatsAppNumber { get; init; }
    public required string Email { get; init; }
    public decimal Latitude { get; init; }
    public decimal Longitude { get; init; }
    public string? GoogleMapsUrl { get; init; }
    public string? HeroImageUrl { get; init; }
    public string? ShortPitch { get; init; }
    public required string WeekdayHours { get; init; }
    public required string WeekendHours { get; init; }
    public int OccupancyCapacity { get; init; }
    public int DisplayOrder { get; init; }
}
