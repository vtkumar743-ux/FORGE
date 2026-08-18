using System.ComponentModel.DataAnnotations;
using Gym.Core.Enums;

namespace Gym.Api.Contracts;

/* ============================================================================
   Read models for the public website (Module 1).

   Everything here is anonymous and read-only except the lead capture. Times are
   returned twice on purpose: the UTC instant for anything that needs ordering or
   a countdown, and the IST wall-clock string for anything the visitor reads. A
   Bengaluru gym's timetable must say "6:30 PM" to a member browsing from Dubai.
   ============================================================================ */

public record ClassFormatResponse
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required string ShortDescription { get; init; }
    public required string Description { get; init; }
    public required int DurationMinutes { get; init; }
    public required int Capacity { get; init; }
    public required ClassLevel Level { get; init; }
    public required string LevelName { get; init; }
    public required ClassIntensity Intensity { get; init; }
    public required string IntensityName { get; init; }
    public int EstimatedCalories { get; init; }
    public string? CoverImageUrl { get; init; }
    public string? IconKey { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
    public int DisplayOrder { get; init; }
    /// <summary>Sessions on the timetable over the next seven days — drives "3 this week".</summary>
    public int UpcomingSessionCount { get; init; }
    /// <summary>Branch slugs that actually run this format, for the rail's location line.</summary>
    public required IReadOnlyList<string> BranchSlugs { get; init; }
    /// <summary>The next bookable session, so a rail card can show a real time, not a placeholder.</summary>
    public ClassSessionResponse? NextSession { get; init; }
}

public record ClassSessionResponse
{
    public required int Id { get; init; }
    public required string FormatName { get; init; }
    public required string FormatSlug { get; init; }
    public string? IconKey { get; init; }
    public string? CoverImageUrl { get; init; }
    public required ClassLevel Level { get; init; }
    public required string LevelName { get; init; }
    public required ClassIntensity Intensity { get; init; }
    public required int BranchId { get; init; }
    public required string BranchName { get; init; }
    public required string BranchSlug { get; init; }
    public required string TrainerName { get; init; }
    public required string TrainerSlug { get; init; }
    public string? TrainerPortraitUrl { get; init; }
    public bool IsSubstitute { get; init; }
    public string? RoomName { get; init; }

    /// <summary>Local (IST) calendar date, ISO yyyy-MM-dd.</summary>
    public required string Date { get; init; }
    /// <summary>Local (IST) wall clock, HH:mm — what the visitor reads.</summary>
    public required string StartTime { get; init; }
    public required string EndTime { get; init; }
    public required DateTime StartsAtUtc { get; init; }
    public required int DurationMinutes { get; init; }

    public required int Capacity { get; init; }
    public required int BookedCount { get; init; }
    public required int SpotsLeft { get; init; }
    public required int WaitlistCount { get; init; }
    public required SessionStatus Status { get; init; }
    /// <summary>False once the class has started or been cancelled — the card drops its CTA.</summary>
    public required bool IsBookable { get; init; }
    /// <summary>Early morning / Morning / Midday / Evening / Late evening — the filter pill value.</summary>
    public required string TimeOfDay { get; init; }
}

public record TimetableResponse
{
    public required string FromDate { get; init; }
    public required string ToDate { get; init; }
    public required IReadOnlyList<ClassSessionResponse> Sessions { get; init; }
    public required IReadOnlyList<TimetableFilterOption> Formats { get; init; }
    public required IReadOnlyList<TimetableFilterOption> Trainers { get; init; }
}

public record TimetableFilterOption
{
    public required string Slug { get; init; }
    public required string Name { get; init; }
    public required int Count { get; init; }
}

public record TrainerResponse
{
    public required int Id { get; init; }
    public required string FullName { get; init; }
    public required string Slug { get; init; }
    public required string Headline { get; init; }
    public required string Bio { get; init; }
    public string? PortraitUrl { get; init; }
    public string? DemoVideoUrl { get; init; }
    public required IReadOnlyList<string> Specialties { get; init; }
    public required IReadOnlyList<string> Certifications { get; init; }
    public int YearsExperience { get; init; }
    public string? InstagramUrl { get; init; }
    public required int BranchId { get; init; }
    public required string BranchName { get; init; }
    public required string BranchSlug { get; init; }
    public decimal PtSessionPrice { get; init; }
    public bool AcceptsPtClients { get; init; }
    public decimal AverageRating { get; init; }
    public int RatingCount { get; init; }
    public int DisplayOrder { get; init; }
    /// <summary>Formats this coach actually teaches, from the live schedule.</summary>
    public required IReadOnlyList<string> TeachesFormats { get; init; }
    public int WeeklyClassCount { get; init; }
}

public record PlanResponse
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required string Tagline { get; init; }
    public required string Description { get; init; }
    public required PlanKind Kind { get; init; }
    public required BillingCycle Cycle { get; init; }
    public required string CycleName { get; init; }
    public required AccessScope AccessScope { get; init; }
    public required int DurationDays { get; init; }
    /// <summary>Price at the requested branch when one was named, otherwise the list price.</summary>
    public required decimal Price { get; init; }
    public required decimal BasePrice { get; init; }
    public required decimal AdmissionFee { get; init; }
    /// <summary>Price ÷ months, so a visitor can compare a ₹19,900 year with a ₹3,200 month.</summary>
    public required decimal EffectiveMonthlyPrice { get; init; }
    /// <summary>Percent saved against paying monthly for the same span. 0 on the monthly plan itself.</summary>
    public required int SavingsPercent { get; init; }
    public decimal GstRatePercent { get; init; }
    public string? SacCode { get; init; }
    public int? ClassCredits { get; init; }
    public int? PtSessionCredits { get; init; }
    public int? GuestPasses { get; init; }
    public int FreezeDaysAllowed { get; init; }
    public string? AccessWindow { get; init; }
    public required IReadOnlyList<string> Features { get; init; }
    public string? TrustMicrocopy { get; init; }
    public bool IsMostPopular { get; init; }
    public bool IsAvailableAtBranch { get; init; }
    public int DisplayOrder { get; init; }
}

public record OfferResponse
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required DiscountType DiscountType { get; init; }
    public required decimal DiscountValue { get; init; }
    public decimal? MaxDiscountAmount { get; init; }
    public required string ValidTo { get; init; }
    public required DateTime ValidToUtc { get; init; }
    public string? BannerHeadline { get; init; }
}

public record TestimonialResponse
{
    public required int Id { get; init; }
    public required string AuthorName { get; init; }
    public string? AuthorRole { get; init; }
    public string? AuthorPhotoUrl { get; init; }
    public required string Quote { get; init; }
    public int Rating { get; init; }
    public string? Program { get; init; }
    public string? BranchName { get; init; }
    public string? BranchSlug { get; init; }
    public bool IsFeatured { get; init; }
}

public record TransformationResponse
{
    public required int Id { get; init; }
    public required string MemberDisplayName { get; init; }
    public required string BeforeImageUrl { get; init; }
    public required string AfterImageUrl { get; init; }
    public required int DurationWeeks { get; init; }
    public required string Program { get; init; }
    public string? TrainerName { get; init; }
    public decimal? WeightBeforeKg { get; init; }
    public decimal? WeightAfterKg { get; init; }
    public string? Story { get; init; }
    public string? BranchName { get; init; }
    public string? BranchSlug { get; init; }
}

public record FaqResponse
{
    public required int Id { get; init; }
    public required string Category { get; init; }
    public required string Question { get; init; }
    public required string Answer { get; init; }
    public int DisplayOrder { get; init; }
}

public record BlogSummaryResponse
{
    public required int Id { get; init; }
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public required string Excerpt { get; init; }
    public string? CoverImageUrl { get; init; }
    public required string AuthorName { get; init; }
    public string? AuthorRole { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
    public int ReadMinutes { get; init; }
    public DateTime? PublishedAtUtc { get; init; }
    public bool IsFeatured { get; init; }
}

public record BlogPostResponse : BlogSummaryResponse
{
    /// <summary>Structured blocks, never raw HTML — the renderer keeps typography on-system.</summary>
    public required IReadOnlyList<BlogBlock> Body { get; init; }
    public required string SeoTitle { get; init; }
    public required string SeoDescription { get; init; }
    public string? OgImageUrl { get; init; }
    public required IReadOnlyList<BlogSummaryResponse> Related { get; init; }
}

public record BlogBlock
{
    public required string Type { get; init; }
    public string? Text { get; init; }
    public string? Url { get; init; }
    public string? Alt { get; init; }
}

/// <summary>
/// Free-trial / tour / PT enquiry from the public site (Module 1.6). Deliberately
/// permissive on everything except name and phone — a lead we cannot contact is
/// worthless, and every extra required field costs conversions.
/// </summary>
public record CreateLeadRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string FullName { get; init; } = string.Empty;

    [Required, StringLength(20, MinimumLength = 8)]
    public string Phone { get; init; } = string.Empty;

    [EmailAddress, StringLength(160)]
    public string? Email { get; init; }

    public string? BranchSlug { get; init; }
    /// <summary>trial | tour | pt | callback — routes the follow-up sequence.</summary>
    public string Intent { get; init; } = "trial";
    [StringLength(60)] public string? Goal { get; init; }
    [StringLength(60)] public string? PreferredTime { get; init; }
    public DateOnly? TrialDate { get; init; }
    [StringLength(1000)] public string? Message { get; init; }
    public bool ConsentMarketing { get; init; }
    [StringLength(60)] public string? PlanSlug { get; init; }
    [StringLength(80)] public string? UtmSource { get; init; }
    [StringLength(80)] public string? UtmCampaign { get; init; }
    /// <summary>Honeypot — bots fill every field, humans never see this one.</summary>
    public string? Website { get; init; }
}

public record CreateLeadResponse
{
    public required int Id { get; init; }
    public required string Reference { get; init; }
    public string? BranchName { get; init; }
    public string? WhatsAppNumber { get; init; }
    /// <summary>When the desk has committed to respond, for the success screen's copy.</summary>
    public required DateTime FirstFollowUpAtUtc { get; init; }
}
