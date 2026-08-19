using Gym.Api.Controllers;
using Gym.Core.Enums;

namespace Gym.Api.Contracts;

/* ============================================================================
   Member portal read/write models (Module 3).

   Every one of these is scoped to the signed-in member server-side, from the
   member_id claim — never from an id in the request body. The client never sends
   "which member": it cannot, and that is the point.
   ============================================================================ */

// ---------------------------------------------------------------- identity

public record PortalMemberResponse
{
    public required int Id { get; init; }
    public required string MemberCode { get; init; }
    public required string FullName { get; init; }
    public string? FirstName { get; init; }
    public string? PhotoUrl { get; init; }
    public string? Email { get; init; }
    public required string Phone { get; init; }
    public required int HomeBranchId { get; init; }
    public required string HomeBranchName { get; init; }
    public required string HomeBranchSlug { get; init; }
    public required string JoinedOn { get; init; }
    public string? PrimaryGoal { get; init; }
    public required MemberStatus Status { get; init; }
    public required string StatusName { get; init; }
    public decimal? HeightCm { get; init; }
    public decimal? StartWeightKg { get; init; }
    public string? DateOfBirth { get; init; }
    public required bool ConsentMarketing { get; init; }
    public required bool ConsentLeaderboard { get; init; }
    public required bool WaiverSigned { get; init; }
}

/// <summary>The digital membership card: what the QR encodes plus what is printed beside it.</summary>
public record PortalCardResponse
{
    public required string MemberCode { get; init; }
    public required string FullName { get; init; }
    public string? PhotoUrl { get; init; }
    /// <summary>The exact string the desk scanner reads; the kiosk matches it against Member.QrToken.</summary>
    public required string QrToken { get; init; }
    public required string HomeBranchName { get; init; }
    public string? PlanName { get; init; }
    public string? ValidUntil { get; init; }
    public int? DaysLeft { get; init; }
    public required string StatusName { get; init; }
    public required bool IsUsable { get; init; }
    /// <summary>Why the card would be refused at the desk, said plainly before they walk in.</summary>
    public string? BlockReason { get; init; }
}

public record UpdatePortalProfileRequest
{
    public string? PrimaryGoal { get; init; }
    public int? HomeBranchId { get; init; }
    public decimal? HeightCm { get; init; }
    public string? EmergencyContactName { get; init; }
    public string? EmergencyContactPhone { get; init; }
    public string? MedicalNotes { get; init; }
    public string? InjuryNotes { get; init; }
    public bool? ConsentMarketing { get; init; }
    public bool? ConsentLeaderboard { get; init; }
}

// ---------------------------------------------------------------- home

public record PortalStreakResponse
{
    public required int CurrentStreakDays { get; init; }
    public required int LongestStreakDays { get; init; }
    public string? LastVisitOn { get; init; }
    public required int VisitsThisWeek { get; init; }
    public required int VisitsThisMonth { get; init; }
    /// <summary>One entry per day over the calendar window, oldest first.</summary>
    public required IReadOnlyList<PortalCalendarDay> Calendar { get; init; }
}

public record PortalCalendarDay
{
    public required string Date { get; init; }
    public required bool Visited { get; init; }
    public required int ClassCount { get; init; }
    public required bool IsToday { get; init; }
}

public record PortalHomeResponse
{
    public required PortalMemberResponse Member { get; init; }
    public PortalMembershipResponse? Membership { get; init; }
    public required PortalStreakResponse Streak { get; init; }
    public BranchOccupancyResponse? HomeBranchOccupancy { get; init; }
    public required IReadOnlyList<PortalSessionResponse> TodaysClasses { get; init; }
    public PortalSessionResponse? NextClass { get; init; }
    public required decimal DuesOutstanding { get; init; }
    public PortalInvoiceRow? NextPayment { get; init; }
    public required int UnreadNotifications { get; init; }
    public required IReadOnlyList<PortalNotificationRow> Announcements { get; init; }
    public required IReadOnlyList<PortalRatingPrompt> RatingPrompts { get; init; }
    public PortalPrCelebration? PendingCelebration { get; init; }
    public required IReadOnlyList<PortalBadgeRow> NewBadges { get; init; }
    public PortalProgramSummary? Program { get; init; }
    public required int ReferralCredits { get; init; }
}

// ---------------------------------------------------------------- booking

public record PortalSessionResponse
{
    public required int Id { get; init; }
    public required string Date { get; init; }
    public required string StartTime { get; init; }
    public required string EndTime { get; init; }
    public required DateTime StartsAtUtc { get; init; }
    public required int DurationMinutes { get; init; }
    public required string FormatName { get; init; }
    public required string FormatSlug { get; init; }
    public string? IconKey { get; init; }
    public required string CoverImageUrl { get; init; }
    public required string LevelName { get; init; }
    public required int EstimatedCalories { get; init; }
    public required int BranchId { get; init; }
    public required string BranchName { get; init; }
    public required string BranchSlug { get; init; }
    public required string TrainerName { get; init; }
    public required string TrainerSlug { get; init; }
    public string? TrainerPortraitUrl { get; init; }
    public required bool IsSubstitute { get; init; }
    public string? RoomName { get; init; }
    public required int Capacity { get; init; }
    public required int BookedCount { get; init; }
    public required int SpotsLeft { get; init; }
    public required int WaitlistCount { get; init; }
    public required SessionStatus Status { get; init; }
    public required string TimeOfDay { get; init; }

    // ---- the member's own relationship with this session
    public int? MyBookingId { get; init; }
    public BookingStatus? MyBookingStatus { get; init; }
    public int? MyWaitlistPosition { get; init; }
    public required bool CanBook { get; init; }
    public required bool CanJoinWaitlist { get; init; }
    public required bool CanCancel { get; init; }
    /// <summary>Set when the member cannot book; shown on the card instead of a dead button.</summary>
    public string? BlockedReason { get; init; }
    public DateTime? BookingOpensAtUtc { get; init; }
    public DateTime? CancelCutoffAtUtc { get; init; }
    /// <summary>True inside the cut-off: cancelling still works but is recorded as a late cancel.</summary>
    public required bool IsLateCancelWindow { get; init; }
    public int? MyRating { get; init; }
}

public record PortalTimetableResponse
{
    public required string FromDate { get; init; }
    public required string ToDate { get; init; }
    public required IReadOnlyList<PortalSessionResponse> Sessions { get; init; }
    public required IReadOnlyList<TimetableFilterOption> Formats { get; init; }
    public required IReadOnlyList<TimetableFilterOption> Trainers { get; init; }
    public required IReadOnlyList<PortalBranchOption> Branches { get; init; }
    /// <summary>Null when the member has no active plan — the booking rail explains rather than fails.</summary>
    public string? BookingBlockedReason { get; init; }
    public int? ClassCreditsRemaining { get; init; }
}

public record PortalBranchOption
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required int Count { get; init; }
    public required bool IsHome { get; init; }
}

public record PortalBookRequest
{
    public required int SessionId { get; init; }
    /// <summary>False means "do not put me on the waitlist if it is full" — an explicit choice.</summary>
    public bool AllowWaitlist { get; init; } = true;
}

public record PortalBookingResponse
{
    public required int BookingId { get; init; }
    public required BookingStatus Status { get; init; }
    public required string StatusName { get; init; }
    public int? WaitlistPosition { get; init; }
    public required int BookedCount { get; init; }
    public required int Capacity { get; init; }
    public required int SpotsLeft { get; init; }
    public required int WaitlistCount { get; init; }
    public required string Headline { get; init; }
    public required string Message { get; init; }
    public int? ClassCreditsRemaining { get; init; }
}

public record PortalBookingRow
{
    public required int Id { get; init; }
    public required int SessionId { get; init; }
    public required BookingStatus Status { get; init; }
    public required string StatusName { get; init; }
    public int? WaitlistPosition { get; init; }
    public required string Date { get; init; }
    public required string StartTime { get; init; }
    public required DateTime StartsAtUtc { get; init; }
    public required int DurationMinutes { get; init; }
    public required string FormatName { get; init; }
    public required string FormatSlug { get; init; }
    public required string CoverImageUrl { get; init; }
    public required string TrainerName { get; init; }
    public required string TrainerSlug { get; init; }
    public string? TrainerPortraitUrl { get; init; }
    public required string BranchName { get; init; }
    public string? RoomName { get; init; }
    public required bool CanCancel { get; init; }
    public required bool IsLateCancelWindow { get; init; }
    public required bool CanRate { get; init; }
    public int? RatingScore { get; init; }
    public string? RatingComment { get; init; }
    public DateTime? CheckedInAtUtc { get; init; }
}

public record PortalRatingPrompt
{
    public required int BookingId { get; init; }
    public required int SessionId { get; init; }
    public required string FormatName { get; init; }
    public required int TrainerId { get; init; }
    public required string TrainerName { get; init; }
    public string? TrainerPortraitUrl { get; init; }
    public required string Date { get; init; }
    public required string StartTime { get; init; }
}

public record PortalRateRequest
{
    /// <summary>1–5.</summary>
    public required int Score { get; init; }
    public string? Comment { get; init; }
}

// ---------------------------------------------------------------- membership & billing

public record PortalMembershipResponse
{
    public required int SubscriptionId { get; init; }
    public required string PlanName { get; init; }
    public required string PlanSlug { get; init; }
    public required string PlanTagline { get; init; }
    public required PlanKind Kind { get; init; }
    public required string CycleName { get; init; }
    public required SubscriptionStatus Status { get; init; }
    public required string StatusName { get; init; }
    public required string BranchName { get; init; }
    public required string AccessScopeName { get; init; }
    public required string StartsOn { get; init; }
    public required string EndsOn { get; init; }
    public required int DaysLeft { get; init; }
    public required int TotalDays { get; init; }
    public required int ClassCreditsRemaining { get; init; }
    public required int PtCreditsRemaining { get; init; }
    public required decimal PriceCharged { get; init; }
    public required bool AutoRenew { get; init; }
    public string? NextBillingOn { get; init; }
    public required int FreezeDaysAllowed { get; init; }
    public required int FreezeDaysUsed { get; init; }
    public required decimal FreezeFee { get; init; }
    public string? FreezeStartsOn { get; init; }
    public string? FreezeEndsOn { get; init; }
    public string? AccessWindow { get; init; }
    public PortalFreezeRequestRow? PendingFreezeRequest { get; init; }
    public required IReadOnlyList<string> Features { get; init; }
}

public record PortalMembershipPageResponse
{
    public PortalMembershipResponse? Current { get; init; }
    public required IReadOnlyList<PortalMembershipHistoryRow> History { get; init; }
    public required IReadOnlyList<PortalInvoiceRow> Invoices { get; init; }
    public required decimal DuesOutstanding { get; init; }
    public required IReadOnlyList<PortalFreezeRequestRow> FreezeRequests { get; init; }
    public required PortalGatewayInfo Gateway { get; init; }
}

public record PortalMembershipHistoryRow
{
    public required int Id { get; init; }
    public required string PlanName { get; init; }
    public required string StartsOn { get; init; }
    public required string EndsOn { get; init; }
    public required string StatusName { get; init; }
    public required decimal PriceCharged { get; init; }
    public required string BranchName { get; init; }
}

public record PortalPlanOption
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required string Tagline { get; init; }
    public required PlanKind Kind { get; init; }
    public required string CycleName { get; init; }
    public required int DurationDays { get; init; }
    public required decimal Price { get; init; }
    public required decimal ListPrice { get; init; }
    public required decimal AdmissionFee { get; init; }
    public required string AccessScopeName { get; init; }
    public required bool IsMostPopular { get; init; }
    public required bool IsCurrentPlan { get; init; }
    public int? ClassCredits { get; init; }
    public int? PtSessionCredits { get; init; }
    public required int FreezeDaysAllowed { get; init; }
    public string? TrustMicrocopy { get; init; }
    public string? AccessWindow { get; init; }
    public required IReadOnlyList<string> Features { get; init; }
}

public record PortalQuoteResponse
{
    public required int PlanId { get; init; }
    public required string PlanName { get; init; }
    public required int BranchId { get; init; }
    public required string BranchName { get; init; }
    public required decimal ListPrice { get; init; }
    public required decimal AdmissionFee { get; init; }
    public required decimal DiscountAmount { get; init; }
    public required decimal ProrationCredit { get; init; }
    public required decimal Payable { get; init; }
    public required decimal GstRatePercent { get; init; }
    public string? CouponCode { get; init; }
    /// <summary>Why a coupon did not apply, so the member can fix it rather than guess.</summary>
    public string? CouponMessage { get; init; }
    public required string StartsOn { get; init; }
    public required string EndsOn { get; init; }
    public required bool IsRenewalOfCurrent { get; init; }
}

public record PortalRenewRequest
{
    public required int PlanId { get; init; }
    public int? BranchId { get; init; }
    public string? CouponCode { get; init; }
    /// <summary>Upgrade mid-term: the unused days on the current plan come back as a credit line.</summary>
    public bool UpgradeNow { get; init; }
    public bool AutoRenew { get; init; }
}

public record PortalCheckoutResponse
{
    public required int InvoiceId { get; init; }
    public required string InvoiceNumber { get; init; }
    public required decimal AmountDue { get; init; }
    public required int SubscriptionId { get; init; }
    public required string StartsOn { get; init; }
    public required string EndsOn { get; init; }
    public PortalOrderResponse? Order { get; init; }
    public required string Headline { get; init; }
    public required string Message { get; init; }
}

public record PortalGatewayInfo
{
    public required string Provider { get; init; }
    public required bool IsLive { get; init; }
    public string? KeyId { get; init; }
    public string? Notice { get; init; }
}

public record PortalOrderResponse
{
    public required string OrderId { get; init; }
    public string? KeyId { get; init; }
    public required decimal AmountInr { get; init; }
    public required string Currency { get; init; }
    public required bool IsSimulated { get; init; }
    public required string PrefillName { get; init; }
    public string? PrefillEmail { get; init; }
    public required string PrefillContact { get; init; }
}

public record PortalInvoiceRow
{
    public required int Id { get; init; }
    public required string InvoiceNumber { get; init; }
    public required string IssuedOn { get; init; }
    public required string DueOn { get; init; }
    public required InvoiceStatus Status { get; init; }
    public required string StatusName { get; init; }
    public required decimal GrandTotal { get; init; }
    public required decimal AmountPaid { get; init; }
    public required decimal AmountDue { get; init; }
    public string? Description { get; init; }
}

public record PortalInvoiceDetail : PortalInvoiceRow
{
    public required string BranchName { get; init; }
    public string? SupplierGstin { get; init; }
    public string? PlaceOfSupply { get; init; }
    public required decimal SubTotal { get; init; }
    public required decimal DiscountTotal { get; init; }
    public required decimal TaxableValue { get; init; }
    public required decimal CgstAmount { get; init; }
    public required decimal SgstAmount { get; init; }
    public required decimal IgstAmount { get; init; }
    public required decimal RoundOff { get; init; }
    public required IReadOnlyList<PortalInvoiceLineRow> Lines { get; init; }
    public required IReadOnlyList<PortalPaymentRow> Payments { get; init; }
}

public record PortalInvoiceLineRow
{
    public required string Description { get; init; }
    public string? SacOrHsnCode { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required decimal DiscountAmount { get; init; }
    public required decimal TaxableValue { get; init; }
    public required decimal GstRatePercent { get; init; }
    public required decimal LineTotal { get; init; }
}

public record PortalPaymentRow
{
    public required int Id { get; init; }
    public required decimal Amount { get; init; }
    public required string ModeName { get; init; }
    public required string StatusName { get; init; }
    public required DateTime PaidAtUtc { get; init; }
    public string? Reference { get; init; }
}

public record PortalFreezeRequest
{
    public required int SubscriptionId { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public required string Reason { get; init; }
}

public record PortalFreezeRequestRow
{
    public required int Id { get; init; }
    public required int SubscriptionId { get; init; }
    public required string PlanName { get; init; }
    public required string RequestedFrom { get; init; }
    public required string RequestedTo { get; init; }
    public required int Days { get; init; }
    public required string Reason { get; init; }
    public required FreezeRequestStatus Status { get; init; }
    public required string StatusName { get; init; }
    public required DateTime RequestedAtUtc { get; init; }
    public DateTime? DecidedAtUtc { get; init; }
    public string? DecisionNote { get; init; }
    public string? MemberName { get; init; }
    public string? MemberCode { get; init; }
}

// ---------------------------------------------------------------- workouts

public record PortalProgramSummary
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public string? Goal { get; init; }
    public required int WeekNumber { get; init; }
    public required int DurationWeeks { get; init; }
    public required int DaysPerWeek { get; init; }
    public string? TrainerName { get; init; }
    public int? NextDayId { get; init; }
    public string? NextDayTitle { get; init; }
    public required int SessionsLogged { get; init; }
}

public record PortalProgramResponse
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Goal { get; init; }
    public required string StatusName { get; init; }
    public required string AuthorName { get; init; }
    public required int DurationWeeks { get; init; }
    public required int DaysPerWeek { get; init; }
    public required int WeekNumber { get; init; }
    public string? StartsOn { get; init; }
    public string? EndsOn { get; init; }
    public string? TrainerName { get; init; }
    public string? TrainerSlug { get; init; }
    public string? TrainerPortraitUrl { get; init; }
    public required IReadOnlyList<PortalProgramDayResponse> Days { get; init; }
}

public record PortalProgramDayResponse
{
    public required int Id { get; init; }
    public required int DayIndex { get; init; }
    public required string Title { get; init; }
    public string? Focus { get; init; }
    public required bool IsRestDay { get; init; }
    public string? Notes { get; init; }
    public required int ExerciseCount { get; init; }
    public required int TotalSets { get; init; }
    public required int EstimatedMinutes { get; init; }
    public string? LastPerformedOn { get; init; }
    public required IReadOnlyList<PortalProgramExerciseResponse> Exercises { get; init; }
}

public record PortalProgramExerciseResponse
{
    public required int Id { get; init; }
    public required int ExerciseId { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required string PrimaryMuscle { get; init; }
    public required string Equipment { get; init; }
    public string? VideoUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? Cues { get; init; }
    public required bool IsStrengthTracked { get; init; }
    public required int OrderIndex { get; init; }
    public required int Sets { get; init; }
    public required string RepScheme { get; init; }
    public required int RestSeconds { get; init; }
    public decimal? TargetWeightKg { get; init; }
    public string? Tempo { get; init; }
    public string? SupersetGroup { get; init; }
    public string? Notes { get; init; }
    /// <summary>What they did last time on this lift — the number the member actually wants.</summary>
    public required IReadOnlyList<PortalSetRow> LastSession { get; init; }
    public string? LastSessionOn { get; init; }
    public decimal? BestE1Rm { get; init; }
    /// <summary>Sets already logged today, so a reload does not lose the session in progress.</summary>
    public required IReadOnlyList<PortalSetRow> TodaySets { get; init; }
}

public record PortalSetRow
{
    public required int Id { get; init; }
    public required int SetNumber { get; init; }
    public required int Reps { get; init; }
    public required decimal WeightKg { get; init; }
    public int? Rpe { get; init; }
    public required decimal Volume { get; init; }
    public required decimal EstimatedOneRepMax { get; init; }
    public required bool IsPersonalRecord { get; init; }
    public required string PerformedOn { get; init; }
    public string? Notes { get; init; }
}

public record PortalLogSetRequest
{
    public required int ExerciseId { get; init; }
    public int? ProgramExerciseId { get; init; }
    public required int SetNumber { get; init; }
    public required int Reps { get; init; }
    public required decimal WeightKg { get; init; }
    public int? Rpe { get; init; }
    public int? DurationSeconds { get; init; }
    public decimal? DistanceKm { get; init; }
    public string? PerformedOn { get; init; }
    public string? Notes { get; init; }
}

public record PortalLogSetResponse
{
    public required PortalSetRow Set { get; init; }
    public required bool IsPersonalRecord { get; init; }
    public decimal? PreviousBestE1Rm { get; init; }
    public PortalPrCelebration? Celebration { get; init; }
    public required IReadOnlyList<PortalBadgeRow> BadgesAwarded { get; init; }
}

/// <summary>The banner payload. Kept server-side so the copy is identical wherever it fires.</summary>
public record PortalPrCelebration
{
    public required int LogId { get; init; }
    public required string ExerciseName { get; init; }
    public required decimal WeightKg { get; init; }
    public required int Reps { get; init; }
    public required decimal EstimatedOneRepMax { get; init; }
    public decimal? PreviousBestE1Rm { get; init; }
    public required string Headline { get; init; }
    public required string Message { get; init; }
    public required string ShareText { get; init; }
    public required string PerformedOn { get; init; }
}

public record PortalWorkoutHistoryRow
{
    public required string Date { get; init; }
    public required int Sets { get; init; }
    public required decimal Volume { get; init; }
    public required int PersonalRecords { get; init; }
    public required IReadOnlyList<string> Exercises { get; init; }
}

// ---------------------------------------------------------------- progress

public record PortalProgressResponse
{
    public required IReadOnlyList<PortalBodyScanRow> Scans { get; init; }
    public required IReadOnlyList<PortalProgressPhotoRow> Photos { get; init; }
    public required IReadOnlyList<PortalStrengthSeries> Strength { get; init; }
    public required IReadOnlyList<PortalVolumePoint> WeeklyVolume { get; init; }
    public required PortalStreakResponse Streak { get; init; }
    public required IReadOnlyList<PortalBadgeRow> Badges { get; init; }
    public required PortalProgressHeadline Headline { get; init; }
}

public record PortalProgressHeadline
{
    public decimal? CurrentWeightKg { get; init; }
    public decimal? WeightChangeKg { get; init; }
    public decimal? CurrentBodyFatPercent { get; init; }
    public decimal? BodyFatChange { get; init; }
    public decimal? MuscleMassChangeKg { get; init; }
    public required int ScanCount { get; init; }
    public string? FirstScanOn { get; init; }
    public string? LatestScanOn { get; init; }
    public required int TotalPersonalRecords { get; init; }
    public required decimal TotalVolumeLiftedKg { get; init; }
}

public record PortalBodyScanRow
{
    public required int Id { get; init; }
    public required string ScanDate { get; init; }
    public required decimal WeightKg { get; init; }
    public decimal? BodyFatPercent { get; init; }
    public decimal? SkeletalMuscleMassKg { get; init; }
    public decimal? FatMassKg { get; init; }
    public decimal? VisceralFatLevel { get; init; }
    public decimal? Bmi { get; init; }
    public decimal? BasalMetabolicRate { get; init; }
    public decimal? TotalBodyWaterL { get; init; }
    public decimal? InBodyScore { get; init; }
    public decimal? ChestCm { get; init; }
    public decimal? WaistCm { get; init; }
    public decimal? HipCm { get; init; }
    public decimal? ThighCm { get; init; }
    public decimal? ArmCm { get; init; }
    public string? MeasuredBy { get; init; }
    public string? DeviceName { get; init; }
    public string? Notes { get; init; }
    /// <summary>True when the member typed it in rather than the desk measuring them.</summary>
    public required bool IsSelfReported { get; init; }
}

public record PortalBodyScanRequest
{
    public required DateOnly ScanDate { get; init; }
    public required decimal WeightKg { get; init; }
    public decimal? BodyFatPercent { get; init; }
    public decimal? SkeletalMuscleMassKg { get; init; }
    public decimal? VisceralFatLevel { get; init; }
    public decimal? ChestCm { get; init; }
    public decimal? WaistCm { get; init; }
    public decimal? HipCm { get; init; }
    public decimal? ThighCm { get; init; }
    public decimal? ArmCm { get; init; }
    public string? Notes { get; init; }
}

public record PortalProgressPhotoRow
{
    public required int Id { get; init; }
    public required string TakenOn { get; init; }
    public required string ImageUrl { get; init; }
    public required string Pose { get; init; }
    public decimal? WeightKg { get; init; }
    public string? Notes { get; init; }
}

public record PortalStrengthSeries
{
    public required int ExerciseId { get; init; }
    public required string ExerciseName { get; init; }
    public required string Slug { get; init; }
    public required decimal BestE1Rm { get; init; }
    public required decimal LatestE1Rm { get; init; }
    public required IReadOnlyList<PortalStrengthPoint> Points { get; init; }
}

public record PortalStrengthPoint
{
    public required string Date { get; init; }
    public required decimal EstimatedOneRepMax { get; init; }
    public required decimal TopSetWeightKg { get; init; }
    public required int TopSetReps { get; init; }
}

public record PortalVolumePoint
{
    public required string WeekStarting { get; init; }
    public required string Label { get; init; }
    public required decimal VolumeKg { get; init; }
    public required int Sets { get; init; }
}

public record PortalBadgeRow
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required string Description { get; init; }
    public required string IconKey { get; init; }
    public required string Tier { get; init; }
    public required DateTime AwardedAtUtc { get; init; }
    public required bool IsSeen { get; init; }
}

// ---------------------------------------------------------------- engagement

public record PortalReferralResponse
{
    public required string Code { get; init; }
    public required string ShareUrl { get; init; }
    public required string ShareMessage { get; init; }
    public required decimal RewardAmount { get; init; }
    public required int Invited { get; init; }
    public required int Joined { get; init; }
    public required int Rewarded { get; init; }
    public required decimal CreditEarned { get; init; }
    public required decimal CreditPending { get; init; }
    public required IReadOnlyList<PortalReferralRow> Rows { get; init; }
}

public record PortalReferralRow
{
    public required int Id { get; init; }
    public string? InviteeName { get; init; }
    public string? InviteePhone { get; init; }
    public required ReferralStatus Status { get; init; }
    public required string StatusName { get; init; }
    public required decimal RewardAmount { get; init; }
    public required bool ReferrerRewarded { get; init; }
    public required DateTime InvitedAtUtc { get; init; }
    public DateTime? ConvertedAtUtc { get; init; }
    public string? ExpiresOn { get; init; }
}

public record PortalInviteRequest
{
    public required string Name { get; init; }
    public required string Phone { get; init; }
    public int? BranchId { get; init; }
}

public record PortalNotificationRow
{
    public required int Id { get; init; }
    public required NotificationKind Kind { get; init; }
    public required string KindName { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public string? ActionUrl { get; init; }
    public required bool IsRead { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}

public record PortalNotificationsResponse
{
    public required IReadOnlyList<PortalNotificationRow> Rows { get; init; }
    public required int Unread { get; init; }
    public required int Total { get; init; }
}
