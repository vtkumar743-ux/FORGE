using System.ComponentModel.DataAnnotations;
using Gym.Core.Enums;

namespace Gym.Api.Contracts;

/* ============================================================================
   Module 2B — operations. Dashboard, members, billing, scheduling, attendance
   and the leads pipeline.
   ============================================================================ */

// ---------------------------------------------------------------- dashboard

public record DashboardResponse
{
    public required DashboardKpis Kpis { get; init; }
    public required IReadOnlyList<BranchComparison> Branches { get; init; }
    public required IReadOnlyList<TimeSeriesPoint> Revenue { get; init; }
    public required IReadOnlyList<TimeSeriesPoint> Footfall { get; init; }
    public required IReadOnlyList<TimeSeriesPoint> Joins { get; init; }
    public required IReadOnlyList<ChurnRiskRow> ChurnRisk { get; init; }
    public required IReadOnlyList<ExpiringRow> Expiring { get; init; }
    public required IReadOnlyList<LeadCard> RecentLeads { get; init; }
    public required IReadOnlyList<PlanMixRow> PlanMix { get; init; }
    public required string GeneratedAtIst { get; init; }
}

public record DashboardKpis
{
    public required int ActiveMembers { get; init; }
    public required int ActiveMembersLastMonth { get; init; }
    /// <summary>Monthly recurring revenue: every live subscription normalised to a month.</summary>
    public required decimal Mrr { get; init; }
    public required decimal MrrLastMonth { get; init; }
    public required int CheckInsToday { get; init; }
    public required int CheckInsYesterday { get; init; }
    public required decimal DuesOutstanding { get; init; }
    public required int DuesInvoiceCount { get; init; }
    public required int ExpiringInSevenDays { get; init; }
    public required int NewLeadsThisWeek { get; init; }
    public required int LeadsAwaitingFirstResponse { get; init; }
    public required int OnFloorNow { get; init; }
    public required decimal RevenueThisMonth { get; init; }
    public required decimal RevenueLastMonth { get; init; }
    public required int ClassesThisWeek { get; init; }
    public required int AtRiskMembers { get; init; }
}

public record BranchComparison
{
    public required int BranchId { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required int ActiveMembers { get; init; }
    public required decimal Mrr { get; init; }
    public required int CheckInsToday { get; init; }
    public required int OnFloorNow { get; init; }
    public required int Capacity { get; init; }
    public required decimal DuesOutstanding { get; init; }
    public required decimal RevenueThisMonth { get; init; }
    /// <summary>Booked spots as a share of capacity across this week's sessions.</summary>
    public required int ClassFillPercent { get; init; }
}

public record TimeSeriesPoint
{
    public required string Label { get; init; }
    public required string Date { get; init; }
    public required decimal Value { get; init; }
    public decimal? Secondary { get; init; }
}

public record ChurnRiskRow
{
    public required int MemberId { get; init; }
    public required string MemberCode { get; init; }
    public required string FullName { get; init; }
    public required string BranchName { get; init; }
    public required ChurnRiskBand Band { get; init; }
    public required int Score { get; init; }
    public string? LastVisitOn { get; init; }
    public required int DaysSinceVisit { get; init; }
    public string? PlanName { get; init; }
    public string? EndsOn { get; init; }
    public required decimal DuesOutstanding { get; init; }
    public required string Phone { get; init; }
}

public record ExpiringRow
{
    public required int SubscriptionId { get; init; }
    public required int MemberId { get; init; }
    public required string MemberCode { get; init; }
    public required string FullName { get; init; }
    public required string Phone { get; init; }
    public required string BranchName { get; init; }
    public required string PlanName { get; init; }
    public required string EndsOn { get; init; }
    public required int DaysLeft { get; init; }
    public required decimal PriceCharged { get; init; }
    public required bool AutoRenew { get; init; }
}

public record PlanMixRow
{
    public required string PlanName { get; init; }
    public required int Subscriptions { get; init; }
    public required decimal Mrr { get; init; }
}

// ---------------------------------------------------------------- members

public record MemberListRow
{
    public required int Id { get; init; }
    public required string MemberCode { get; init; }
    public required string FullName { get; init; }
    public required string Phone { get; init; }
    public string? Email { get; init; }
    public string? PhotoUrl { get; init; }
    public required int BranchId { get; init; }
    public required string BranchName { get; init; }
    public required MemberStatus Status { get; init; }
    public required string JoinedOn { get; init; }
    public string? PlanName { get; init; }
    public string? MembershipEndsOn { get; init; }
    public int? DaysLeft { get; init; }
    public required decimal DuesOutstanding { get; init; }
    public string? LastVisitOn { get; init; }
    public required int CurrentStreakDays { get; init; }
    public required ChurnRiskBand ChurnRisk { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public string? DateOfBirth { get; init; }
}

public record MemberDetailResponse
{
    public required MemberListRow Summary { get; init; }
    public required MemberProfile Profile { get; init; }
    public required IReadOnlyList<SubscriptionRow> Subscriptions { get; init; }
    public required IReadOnlyList<InvoiceRow> Invoices { get; init; }
    public required IReadOnlyList<BookingRow> UpcomingBookings { get; init; }
    public required IReadOnlyList<TimelineEntry> Timeline { get; init; }
    public required MemberStats Stats { get; init; }
}

public record MemberProfile
{
    public required Gender Gender { get; init; }
    public string? DateOfBirth { get; init; }
    public string? AddressLine { get; init; }
    public string? City { get; init; }
    public string? Pincode { get; init; }
    public string? PrimaryGoal { get; init; }
    public string? MedicalNotes { get; init; }
    public string? InjuryNotes { get; init; }
    public decimal? HeightCm { get; init; }
    public decimal? StartWeightKg { get; init; }
    public string? EmergencyContactName { get; init; }
    public string? EmergencyContactPhone { get; init; }
    public required bool WaiverSigned { get; init; }
    public string? WaiverDocumentUrl { get; init; }
    public DateTime? WaiverSignedAtUtc { get; init; }
    public required string QrToken { get; init; }
    public string? ReferralCode { get; init; }
    public string? CorporateCode { get; init; }
    public required bool ConsentMarketing { get; init; }
    public required bool ConsentLeaderboard { get; init; }
    public required bool ConsentTransformationShowcase { get; init; }
}

public record MemberStats
{
    public required int TotalVisits { get; init; }
    public required int VisitsLast30Days { get; init; }
    public required int ClassesAttended { get; init; }
    public required int NoShows { get; init; }
    public required int LongestStreakDays { get; init; }
    public required decimal LifetimeValue { get; init; }
    public required int ChurnScore { get; init; }
}

public record BookingRow
{
    public required int Id { get; init; }
    public required int SessionId { get; init; }
    public required string FormatName { get; init; }
    public required string BranchName { get; init; }
    public required string TrainerName { get; init; }
    public required string Date { get; init; }
    public required string StartTime { get; init; }
    public required BookingStatus Status { get; init; }
    public int? WaitlistPosition { get; init; }
}

public record TimelineEntry
{
    public required DateTime AtUtc { get; init; }
    public required string Kind { get; init; }
    public required string Title { get; init; }
    public string? Detail { get; init; }
    public string? Amount { get; init; }
}

public record UpsertMemberRequest
{
    [Required, MaxLength(160)] public string FullName { get; init; } = string.Empty;
    [Required, MaxLength(20)] public string Phone { get; init; } = string.Empty;
    [EmailAddress] public string? Email { get; init; }
    [Required] public int HomeBranchId { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public Gender Gender { get; init; }
    public string? PhotoUrl { get; init; }
    public string? AddressLine { get; init; }
    public string? City { get; init; }
    public string? Pincode { get; init; }
    public MemberStatus Status { get; init; } = MemberStatus.Lead;
    public string? PrimaryGoal { get; init; }
    public string? MedicalNotes { get; init; }
    public string? InjuryNotes { get; init; }
    public decimal? HeightCm { get; init; }
    public decimal? StartWeightKg { get; init; }
    public string? EmergencyContactName { get; init; }
    public string? EmergencyContactPhone { get; init; }
    public bool WaiverSigned { get; init; }
    public string? WaiverDocumentUrl { get; init; }
    public string? Tags { get; init; }
    public string? CorporateCode { get; init; }
    public bool ConsentMarketing { get; init; } = true;
    public bool ConsentLeaderboard { get; init; }
    public bool ConsentTransformationShowcase { get; init; }
    /// <summary>Only used on create; a portal login is optional at the desk.</summary>
    public string? InitialPassword { get; init; }
}

public record BulkMemberRequest
{
    [Required, MinLength(1)] public int[] MemberIds { get; init; } = Array.Empty<int>();
    /// <summary>addTag | removeTag | setStatus | setBranch</summary>
    [Required] public string Action { get; init; } = string.Empty;
    public string? Tag { get; init; }
    public MemberStatus? Status { get; init; }
    public int? BranchId { get; init; }
}

// ---------------------------------------------------------------- billing

public record PlanRow
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required string Tagline { get; init; }
    public string? Description { get; init; }
    public required PlanKind Kind { get; init; }
    public required BillingCycle Cycle { get; init; }
    public required AccessScope AccessScope { get; init; }
    public required int DurationDays { get; init; }
    public required decimal BasePrice { get; init; }
    public required decimal AdmissionFee { get; init; }
    public required decimal GstRatePercent { get; init; }
    public required string SacCode { get; init; }
    public int? ClassCredits { get; init; }
    public int? PtSessionCredits { get; init; }
    public int? GuestPasses { get; init; }
    public required int FreezeDaysAllowed { get; init; }
    public required decimal FreezeFee { get; init; }
    public string? AccessWindowStart { get; init; }
    public string? AccessWindowEnd { get; init; }
    public IReadOnlyList<string> Features { get; init; } = Array.Empty<string>();
    public string? TrustMicrocopy { get; init; }
    public required bool IsMostPopular { get; init; }
    public required bool ShowOnWebsite { get; init; }
    public required bool IsActive { get; init; }
    public required int DisplayOrder { get; init; }
    public required int ActiveSubscriptions { get; init; }
    public required IReadOnlyList<PlanBranchPriceRow> BranchPrices { get; init; }
}

public record PlanBranchPriceRow
{
    public required int BranchId { get; init; }
    public required string BranchName { get; init; }
    public required decimal Price { get; init; }
    public decimal? AdmissionFee { get; init; }
    public required bool IsAvailable { get; init; }
}

public record UpsertPlanRequest
{
    [Required, MaxLength(120)] public string Name { get; init; } = string.Empty;
    [Required, MaxLength(120)] public string Slug { get; init; } = string.Empty;
    [Required] public string Tagline { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public PlanKind Kind { get; init; }
    public BillingCycle Cycle { get; init; }
    public AccessScope AccessScope { get; init; }
    [Range(1, 3650)] public int DurationDays { get; init; } = 30;
    [Range(0, 1_000_000)] public decimal BasePrice { get; init; }
    [Range(0, 1_000_000)] public decimal AdmissionFee { get; init; }
    [Range(0, 28)] public decimal GstRatePercent { get; init; } = 5m;
    public string SacCode { get; init; } = "999723";
    public int? ClassCredits { get; init; }
    public int? PtSessionCredits { get; init; }
    public int? GuestPasses { get; init; }
    public int FreezeDaysAllowed { get; init; }
    public decimal FreezeFee { get; init; }
    public TimeOnly? AccessWindowStart { get; init; }
    public TimeOnly? AccessWindowEnd { get; init; }
    public string[] Features { get; init; } = Array.Empty<string>();
    public string? TrustMicrocopy { get; init; }
    public bool IsMostPopular { get; init; }
    public bool ShowOnWebsite { get; init; } = true;
    public bool IsActive { get; init; } = true;
    public int DisplayOrder { get; init; }
}

public record SetBranchPricesRequest
{
    [Required] public BranchPriceInput[] Prices { get; init; } = Array.Empty<BranchPriceInput>();
}

public record BranchPriceInput
{
    [Required] public int BranchId { get; init; }
    [Range(0, 1_000_000)] public decimal Price { get; init; }
    public decimal? AdmissionFee { get; init; }
    public bool IsAvailable { get; init; } = true;
}

public record CouponRow
{
    public required int Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required DiscountType DiscountType { get; init; }
    public required decimal DiscountValue { get; init; }
    public decimal? MaxDiscountAmount { get; init; }
    public required decimal MinOrderAmount { get; init; }
    public required string ValidFrom { get; init; }
    public required string ValidTo { get; init; }
    public int? UsageCap { get; init; }
    public required int UsageCount { get; init; }
    public int? PerMemberCap { get; init; }
    public string? BranchScope { get; init; }
    public string? PlanScope { get; init; }
    public required bool IsActive { get; init; }
    public required bool ShowAsWebsiteBanner { get; init; }
    public string? BannerHeadline { get; init; }
    public required bool IsLive { get; init; }
}

public record UpsertCouponRequest
{
    [Required, MaxLength(40)] public string Code { get; init; } = string.Empty;
    [Required, MaxLength(120)] public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DiscountType DiscountType { get; init; }
    [Range(0, 1_000_000)] public decimal DiscountValue { get; init; }
    public decimal? MaxDiscountAmount { get; init; }
    public decimal MinOrderAmount { get; init; }
    [Required] public DateOnly ValidFrom { get; init; }
    [Required] public DateOnly ValidTo { get; init; }
    public int? UsageCap { get; init; }
    public int? PerMemberCap { get; init; } = 1;
    public string? BranchScope { get; init; }
    public string? PlanScope { get; init; }
    public bool IsActive { get; init; } = true;
    public bool ShowAsWebsiteBanner { get; init; }
    public string? BannerHeadline { get; init; }
}

public record SubscriptionRow
{
    public required int Id { get; init; }
    public required int MemberId { get; init; }
    public required string MemberName { get; init; }
    public required string MemberCode { get; init; }
    public required int PlanId { get; init; }
    public required string PlanName { get; init; }
    public required string BranchName { get; init; }
    public required SubscriptionStatus Status { get; init; }
    public required string StartsOn { get; init; }
    public required string EndsOn { get; init; }
    public required int DaysLeft { get; init; }
    public required decimal PriceCharged { get; init; }
    public required decimal DiscountAmount { get; init; }
    public required int ClassCreditsRemaining { get; init; }
    public required int PtCreditsRemaining { get; init; }
    public string? FreezeStartsOn { get; init; }
    public string? FreezeEndsOn { get; init; }
    public required int FreezeDaysUsed { get; init; }
    public required int FreezeDaysAllowed { get; init; }
    public required bool AutoRenew { get; init; }
    public string? CancellationReason { get; init; }
}

public record SellPlanRequest
{
    [Required] public int MemberId { get; init; }
    [Required] public int PlanId { get; init; }
    [Required] public int BranchId { get; init; }
    public DateOnly? StartsOn { get; init; }
    public string? CouponCode { get; init; }
    public int? UpgradeFromSubscriptionId { get; init; }
    public bool AutoRenew { get; init; }
    [Range(0, 90)] public int DueInDays { get; init; }
    public string? Notes { get; init; }
    /// <summary>Optional immediate collection so a desk sale is one action, not two.</summary>
    public PaymentMode? CollectMode { get; init; }
    public decimal? CollectAmount { get; init; }
}

public record FreezeSubscriptionRequest
{
    [Required] public DateOnly From { get; init; }
    [Required] public DateOnly To { get; init; }
    public string? Reason { get; init; }
}

public record InvoiceRow
{
    public required int Id { get; init; }
    public required string InvoiceNumber { get; init; }
    public required int MemberId { get; init; }
    public required string MemberName { get; init; }
    public required string MemberCode { get; init; }
    public required string BranchName { get; init; }
    public required string IssuedOn { get; init; }
    public required string DueOn { get; init; }
    public required InvoiceStatus Status { get; init; }
    public required decimal GrandTotal { get; init; }
    public required decimal AmountPaid { get; init; }
    public required decimal AmountDue { get; init; }
    public required int RemindersSent { get; init; }
    public required int DaysOverdue { get; init; }
    public string? PlanName { get; init; }
}

public record InvoiceDetailResponse
{
    public required InvoiceRow Header { get; init; }
    public required IReadOnlyList<InvoiceLineRow> Lines { get; init; }
    public required IReadOnlyList<PaymentRow> Payments { get; init; }
    public required decimal SubTotal { get; init; }
    public required decimal DiscountTotal { get; init; }
    public required decimal TaxableValue { get; init; }
    public required decimal CgstAmount { get; init; }
    public required decimal SgstAmount { get; init; }
    public required decimal IgstAmount { get; init; }
    public required decimal RoundOff { get; init; }
    public string? SupplierGstin { get; init; }
    public string? PlaceOfSupply { get; init; }
    public string? CustomerGstin { get; init; }
    public string? Notes { get; init; }
    public required string BranchAddress { get; init; }
    public required string MemberPhone { get; init; }
    public string? MemberEmail { get; init; }
}

public record InvoiceLineRow
{
    public required int Id { get; init; }
    public required string Description { get; init; }
    public string? SacOrHsnCode { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal DiscountAmount { get; init; }
    public required decimal TaxableValue { get; init; }
    public required decimal GstRatePercent { get; init; }
    public required decimal CgstAmount { get; init; }
    public required decimal SgstAmount { get; init; }
    public required decimal IgstAmount { get; init; }
    public required decimal LineTotal { get; init; }
}

public record PaymentRow
{
    public required int Id { get; init; }
    public required decimal Amount { get; init; }
    public required PaymentMode Mode { get; init; }
    public required PaymentStatus Status { get; init; }
    public required DateTime PaidAtUtc { get; init; }
    public string? GatewayName { get; init; }
    public string? GatewayPaymentId { get; init; }
    public string? ChequeNumber { get; init; }
    public string? BankReference { get; init; }
    public string? ReceivedBy { get; init; }
    public string? Notes { get; init; }
}

public record RecordPaymentRequest
{
    [Required] public int InvoiceId { get; init; }
    [Range(0.01, 10_000_000)] public decimal Amount { get; init; }
    [Required] public PaymentMode Mode { get; init; }
    public string? ChequeNumber { get; init; }
    public string? BankReference { get; init; }
    public string? Notes { get; init; }
    /// <summary>Client-generated so a double-submitted form cannot double-credit.</summary>
    public string? IdempotencyKey { get; init; }
}

public record CreateOrderRequest
{
    [Required] public int InvoiceId { get; init; }
    /// <summary>Defaults to the full amount outstanding.</summary>
    public decimal? Amount { get; init; }
}

public record CreateOrderResponse
{
    public required string OrderId { get; init; }
    public string? KeyId { get; init; }
    public required decimal AmountInr { get; init; }
    public required string Currency { get; init; }
    public required string Receipt { get; init; }
    public required bool IsSimulated { get; init; }
    public required string InvoiceNumber { get; init; }
    public required string MemberName { get; init; }
    public string? MemberEmail { get; init; }
    public required string MemberPhone { get; init; }
    public string? Notice { get; init; }
}

public record VerifyPaymentRequest
{
    [Required] public int InvoiceId { get; init; }
    [Required] public string RazorpayOrderId { get; init; } = string.Empty;
    [Required] public string RazorpayPaymentId { get; init; } = string.Empty;
    public string RazorpaySignature { get; init; } = string.Empty;
    public decimal? Amount { get; init; }
}

// ---------------------------------------------------------------- scheduling

public record ClassFormatRow
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required string ShortDescription { get; init; }
    public string? Description { get; init; }
    public required int DefaultDurationMinutes { get; init; }
    public required int DefaultCapacity { get; init; }
    public required ClassLevel Level { get; init; }
    public required ClassIntensity Intensity { get; init; }
    public required int EstimatedCalories { get; init; }
    public string? CoverImageUrl { get; init; }
    public string? IconKey { get; init; }
    public string? Tags { get; init; }
    public required bool ShowOnWebsite { get; init; }
    public required bool IsActive { get; init; }
    public required int DisplayOrder { get; init; }
    public required int WeeklySlots { get; init; }
}

public record UpsertClassFormatRequest
{
    [Required, MaxLength(120)] public string Name { get; init; } = string.Empty;
    [Required, MaxLength(120)] public string Slug { get; init; } = string.Empty;
    [Required] public string ShortDescription { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    [Range(10, 240)] public int DefaultDurationMinutes { get; init; } = 45;
    [Range(1, 200)] public int DefaultCapacity { get; init; } = 20;
    public ClassLevel Level { get; init; }
    public ClassIntensity Intensity { get; init; }
    [Range(0, 2000)] public int EstimatedCalories { get; init; }
    public string? CoverImageUrl { get; init; }
    public string? IconKey { get; init; }
    public string? Tags { get; init; }
    public bool ShowOnWebsite { get; init; } = true;
    public bool IsActive { get; init; } = true;
    public int DisplayOrder { get; init; }
}

public record RoomRow
{
    public required int Id { get; init; }
    public required int BranchId { get; init; }
    public required string BranchName { get; init; }
    public required string Name { get; init; }
    public required int Capacity { get; init; }
    public string? Notes { get; init; }
    public required bool IsActive { get; init; }
}

public record UpsertRoomRequest
{
    [Required] public int BranchId { get; init; }
    [Required, MaxLength(120)] public string Name { get; init; } = string.Empty;
    [Range(1, 500)] public int Capacity { get; init; } = 20;
    public string? Notes { get; init; }
    public bool IsActive { get; init; } = true;
}

public record ScheduleRow
{
    public required int Id { get; init; }
    public required int BranchId { get; init; }
    public required string BranchName { get; init; }
    public required int ClassFormatId { get; init; }
    public required string FormatName { get; init; }
    public string? IconKey { get; init; }
    public int? RoomId { get; init; }
    public string? RoomName { get; init; }
    public required int TrainerId { get; init; }
    public required string TrainerName { get; init; }
    public required DayOfWeek DayOfWeek { get; init; }
    public required string StartTime { get; init; }
    public required string EndTime { get; init; }
    public required int DurationMinutes { get; init; }
    public required int Capacity { get; init; }
    public required string EffectiveFrom { get; init; }
    public string? EffectiveTo { get; init; }
    public required int BookingOpensHoursBefore { get; init; }
    public required int CancelCutoffHoursBefore { get; init; }
    public required bool WaitlistEnabled { get; init; }
    public required int WaitlistCapacity { get; init; }
    public required bool IsActive { get; init; }
    public required int UpcomingSessions { get; init; }
    public required int AverageFillPercent { get; init; }
}

public record UpsertScheduleRequest
{
    [Required] public int BranchId { get; init; }
    [Required] public int ClassFormatId { get; init; }
    public int? RoomId { get; init; }
    [Required] public int TrainerId { get; init; }
    [Required] public DayOfWeek DayOfWeek { get; init; }
    [Required] public TimeOnly StartTime { get; init; }
    [Range(10, 240)] public int DurationMinutes { get; init; } = 45;
    [Range(1, 200)] public int Capacity { get; init; } = 20;
    [Required] public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    [Range(1, 336)] public int BookingOpensHoursBefore { get; init; } = 72;
    [Range(0, 72)] public int CancelCutoffHoursBefore { get; init; } = 4;
    public bool WaitlistEnabled { get; init; } = true;
    [Range(0, 100)] public int WaitlistCapacity { get; init; } = 10;
    public bool IsActive { get; init; } = true;
    /// <summary>Weeks of occurrences to materialise straight away.</summary>
    [Range(0, 12)] public int MaterialiseWeeks { get; init; } = 4;
    /// <summary>Set once the owner has read the conflict list and still wants the slot.</summary>
    public bool IgnoreConflicts { get; init; }
}

public record ConflictCheckRequest
{
    [Required] public int BranchId { get; init; }
    [Required] public int TrainerId { get; init; }
    public int? RoomId { get; init; }
    [Required] public DayOfWeek DayOfWeek { get; init; }
    [Required] public TimeOnly StartTime { get; init; }
    [Range(10, 240)] public int DurationMinutes { get; init; } = 45;
    [Required] public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public int? IgnoreScheduleId { get; init; }
}

public record ConflictRow
{
    public required string Kind { get; init; }
    public required string Message { get; init; }
    public required int ConflictingScheduleId { get; init; }
    public required string ConflictingLabel { get; init; }
}

public record AdminSessionRow
{
    public required int Id { get; init; }
    public required string Date { get; init; }
    public required string StartTime { get; init; }
    public required string EndTime { get; init; }
    public required string FormatName { get; init; }
    public required string BranchName { get; init; }
    public required int BranchId { get; init; }
    public required string TrainerName { get; init; }
    public required bool IsSubstitute { get; init; }
    public string? RoomName { get; init; }
    public required int Capacity { get; init; }
    public required int BookedCount { get; init; }
    public required int WaitlistCount { get; init; }
    public required int AttendedCount { get; init; }
    public required SessionStatus Status { get; init; }
    public string? CancellationReason { get; init; }
    public required int FillPercent { get; init; }
}

public record RosterEntry
{
    public required int BookingId { get; init; }
    public required int MemberId { get; init; }
    public required string MemberCode { get; init; }
    public required string FullName { get; init; }
    public string? PhotoUrl { get; init; }
    public required string Phone { get; init; }
    public required BookingStatus Status { get; init; }
    public int? WaitlistPosition { get; init; }
    public DateTime? CheckedInAtUtc { get; init; }
    public required DateTime BookedAtUtc { get; init; }
    public required bool WasPromoted { get; init; }
    public required int NoShowsLast90Days { get; init; }
}

public record BookMemberRequest
{
    [Required] public int SessionId { get; init; }
    [Required] public int MemberId { get; init; }
    /// <summary>Adds to the waitlist rather than failing when the class is full.</summary>
    public bool AllowWaitlist { get; init; } = true;
}

public record CancelSessionRequest
{
    [Required] public string Reason { get; init; } = string.Empty;
    public bool NotifyMembers { get; init; } = true;
}

// ---------------------------------------------------------------- attendance

public record CheckInRequest
{
    /// <summary>Either the scanned QR payload or a member picked from the desk search.</summary>
    public string? QrToken { get; init; }
    public int? MemberId { get; init; }
    [Required] public int BranchId { get; init; }
    public int? ClassSessionId { get; init; }
    public CheckInSource Source { get; init; } = CheckInSource.Kiosk;
    public string? DeviceId { get; init; }
    /// <summary>Records the visit even when access rules say no — a manual desk override.</summary>
    public bool Override { get; init; }
}

public record CheckInResponse
{
    public required bool Admitted { get; init; }
    public required string Headline { get; init; }
    public required string Message { get; init; }
    public int? CheckInId { get; init; }
    public int? MemberId { get; init; }
    public string? MemberCode { get; init; }
    public string? FullName { get; init; }
    public string? PhotoUrl { get; init; }
    public string? PlanName { get; init; }
    public string? MembershipEndsOn { get; init; }
    public int? DaysLeft { get; init; }
    public decimal DuesOutstanding { get; init; }
    public int CurrentStreakDays { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
    public IReadOnlyList<AdminSessionRow> TodaysClasses { get; init; } = Array.Empty<AdminSessionRow>();
    public string? BlockReason { get; init; }
}

public record AttendanceRow
{
    public required int Id { get; init; }
    public required int MemberId { get; init; }
    public required string MemberCode { get; init; }
    public required string FullName { get; init; }
    public string? PhotoUrl { get; init; }
    public required string BranchName { get; init; }
    public required DateTime CheckInAtUtc { get; init; }
    public DateTime? CheckOutAtUtc { get; init; }
    public int? DurationMinutes { get; init; }
    public required CheckInSource Source { get; init; }
    public required bool WasBlocked { get; init; }
    public string? BlockReason { get; init; }
    public string? ClassName { get; init; }
}

public record HeatmapResponse
{
    /// <summary>Rows are weekdays 0–6, columns are hours 5–23; value is the visit count.</summary>
    public required IReadOnlyList<HeatmapCell> Cells { get; init; }
    public required int PeakCount { get; init; }
    public required string PeakLabel { get; init; }
    public required int TotalVisits { get; init; }
    public required int DaysCovered { get; init; }
    public required IReadOnlyList<TimeSeriesPoint> Daily { get; init; }
}

public record HeatmapCell
{
    public required int DayOfWeek { get; init; }
    public required int Hour { get; init; }
    public required int Count { get; init; }
}

public record AbsenteeRow
{
    public required int MemberId { get; init; }
    public required string MemberCode { get; init; }
    public required string FullName { get; init; }
    public required string Phone { get; init; }
    public required string BranchName { get; init; }
    public string? LastVisitOn { get; init; }
    public required int DaysSinceVisit { get; init; }
    public string? PlanName { get; init; }
    public string? MembershipEndsOn { get; init; }
    public required bool WinBackSent { get; init; }
    public DateTime? WinBackSentAtUtc { get; init; }
}

// ---------------------------------------------------------------- leads

public record LeadCard
{
    public required int Id { get; init; }
    public required string Reference { get; init; }
    public required string FullName { get; init; }
    public required string Phone { get; init; }
    public string? Email { get; init; }
    public int? BranchId { get; init; }
    public string? BranchName { get; init; }
    public required LeadStage Stage { get; init; }
    public required LeadSource Source { get; init; }
    public string? SourceDetail { get; init; }
    public string? Goal { get; init; }
    public string? InterestedPlanName { get; init; }
    public string? TrialRequestedFor { get; init; }
    public string? AssignedTo { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public DateTime? FirstResponseAtUtc { get; init; }
    public DateTime? NextFollowUpAtUtc { get; init; }
    public required bool IsOverdue { get; init; }
    public required int AgeDays { get; init; }
    public required int OpenFollowUps { get; init; }
    public int? ConvertedMemberId { get; init; }
    public string? LostReason { get; init; }
}

public record LeadBoardResponse
{
    public required IReadOnlyList<LeadColumn> Columns { get; init; }
    public required LeadStats Stats { get; init; }
}

public record LeadColumn
{
    public required LeadStage Stage { get; init; }
    public required string Name { get; init; }
    public required int Total { get; init; }
    public required IReadOnlyList<LeadCard> Cards { get; init; }
}

public record LeadStats
{
    public required int NewThisWeek { get; init; }
    public required int AwaitingFirstResponse { get; init; }
    public required int OverdueFollowUps { get; init; }
    public required int JoinedThisMonth { get; init; }
    public required decimal ConversionRate { get; init; }
    public required decimal MedianFirstResponseMinutes { get; init; }
    public required IReadOnlyList<SourceBreakdown> BySource { get; init; }
}

public record SourceBreakdown
{
    public required LeadSource Source { get; init; }
    public required string Name { get; init; }
    public required int Total { get; init; }
    public required int Joined { get; init; }
    public required decimal ConversionRate { get; init; }
}

public record LeadDetailResponse
{
    public required LeadCard Card { get; init; }
    public string? Message { get; init; }
    public string? PreferredTime { get; init; }
    public string? UtmSource { get; init; }
    public string? UtmCampaign { get; init; }
    public required bool SequenceActive { get; init; }
    public required int SequenceStep { get; init; }
    public required IReadOnlyList<FollowUpRow> FollowUps { get; init; }
}

public record FollowUpRow
{
    public required int Id { get; init; }
    public required FollowUpChannel Channel { get; init; }
    public required DateTime DueAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public string? Outcome { get; init; }
    public string? Notes { get; init; }
    public string? Owner { get; init; }
    public required bool IsAutomated { get; init; }
    public required bool IsOverdue { get; init; }
}

public record UpdateLeadRequest
{
    public string? FullName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public int? BranchId { get; init; }
    public LeadSource? Source { get; init; }
    public string? Goal { get; init; }
    public string? Message { get; init; }
    public string? AssignedTo { get; init; }
    public int? InterestedPlanId { get; init; }
    public DateOnly? TrialRequestedFor { get; init; }
    public bool? SequenceActive { get; init; }
}

public record MoveLeadRequest
{
    [Required] public LeadStage Stage { get; init; }
    public string? LostReason { get; init; }
    public string? Note { get; init; }
}

public record CreateFollowUpRequest
{
    [Required] public FollowUpChannel Channel { get; init; }
    [Required] public DateTime DueAtUtc { get; init; }
    public string? Notes { get; init; }
    public string? Owner { get; init; }
}

public record CompleteFollowUpRequest
{
    [Required] public string Outcome { get; init; } = string.Empty;
    public string? Notes { get; init; }
    /// <summary>Schedules the next touch in the same action, which is how a desk actually works.</summary>
    public DateTime? NextDueAtUtc { get; init; }
    public FollowUpChannel? NextChannel { get; init; }
}

public record ConvertLeadRequest
{
    [Required] public int BranchId { get; init; }
    public string? Email { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public Gender Gender { get; init; }
    /// <summary>Sells the plan in the same step when supplied.</summary>
    public int? PlanId { get; init; }
    public DateOnly? StartsOn { get; init; }
    public string? CouponCode { get; init; }
    public PaymentMode? CollectMode { get; init; }
    public decimal? CollectAmount { get; init; }
    public string? InitialPassword { get; init; }
}

/// <summary>The desk's answer to a member's freeze ask; a decline should carry a reason.</summary>
public record DecideFreezeRequest
{
    public required bool Approve { get; init; }
    public string? Note { get; init; }
}
