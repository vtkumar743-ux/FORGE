using Gym.Core.Enums;

namespace Gym.Core.Entities;

/// <summary>Central plan catalogue; per-branch money lives in <see cref="PlanBranchPrice"/>.</summary>
public class Plan : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Tagline { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PlanKind Kind { get; set; }
    public BillingCycle Cycle { get; set; }
    public AccessScope AccessScope { get; set; }

    public int DurationDays { get; set; }
    /// <summary>List price at network level, in INR. Branch overrides win when present.</summary>
    public decimal BasePrice { get; set; }
    public decimal AdmissionFee { get; set; }
    public decimal GstRatePercent { get; set; } = 5m;
    /// <summary>SAC code for gym services — 999723 (health & fitness).</summary>
    public string SacCode { get; set; } = "999723";

    public int? ClassCredits { get; set; }
    public int? PtSessionCredits { get; set; }
    public int? GuestPasses { get; set; }
    public int FreezeDaysAllowed { get; set; }
    public decimal FreezeFee { get; set; }

    /// <summary>Off-peak tier: access restricted to this window (India-context differentiator).</summary>
    public TimeOnly? AccessWindowStart { get; set; }
    public TimeOnly? AccessWindowEnd { get; set; }

    /// <summary>JSON array of feature strings rendered on the pricing card.</summary>
    public string FeaturesJson { get; set; } = "[]";
    public string? TrustMicrocopy { get; set; }
    public bool IsMostPopular { get; set; }
    public bool ShowOnWebsite { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    public ICollection<PlanBranchPrice> BranchPrices { get; set; } = new List<PlanBranchPrice>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}

/// <summary>Metro vs tier-2 pricing without cloning the plan.</summary>
public class PlanBranchPrice : BaseEntity
{
    public int PlanId { get; set; }
    public Plan Plan { get; set; } = null!;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    public decimal Price { get; set; }
    public decimal? AdmissionFee { get; set; }
    public bool IsAvailable { get; set; } = true;
}

public class Subscription : BaseEntity
{
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public int PlanId { get; set; }
    public Plan Plan { get; set; } = null!;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public DateOnly? CancelledOn { get; set; }
    public string? CancellationReason { get; set; }

    public decimal PriceCharged { get; set; }
    public decimal DiscountAmount { get; set; }
    public int? CouponId { get; set; }
    public Coupon? Coupon { get; set; }

    public int ClassCreditsRemaining { get; set; }
    public int PtCreditsRemaining { get; set; }

    public DateOnly? FreezeStartsOn { get; set; }
    public DateOnly? FreezeEndsOn { get; set; }
    public int FreezeDaysUsed { get; set; }

    public bool AutoRenew { get; set; }
    /// <summary>Razorpay subscription/mandate id once e-mandates are enabled.</summary>
    public string? GatewayMandateRef { get; set; }
    public DateOnly? NextBillingOn { get; set; }
    public int? UpgradedFromSubscriptionId { get; set; }
    public Subscription? UpgradedFromSubscription { get; set; }

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}

public class Coupon : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public decimal MinOrderAmount { get; set; }

    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; }
    public int? UsageCap { get; set; }
    public int UsageCount { get; set; }
    public int? PerMemberCap { get; set; } = 1;
    /// <summary>Null = all branches. Comma-separated branch ids otherwise.</summary>
    public string? BranchScope { get; set; }
    public string? PlanScope { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Drives the CMS-controlled seasonal-offer banner on the public site.</summary>
    public bool ShowAsWebsiteBanner { get; set; }
    public string? BannerHeadline { get; set; }
}

public class Invoice : BaseEntity
{
    /// <summary>Sequential, gap-free per financial year: FRG/25-26/000412.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public int? SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }
    public int? OrderId { get; set; }
    public Order? Order { get; set; }

    public DateOnly IssuedOn { get; set; }
    public DateOnly DueOn { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Issued;

    public decimal SubTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal RoundOff { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountDue { get; set; }

    public string? SupplierGstin { get; set; }
    public string? PlaceOfSupply { get; set; }
    public string? CustomerGstin { get; set; }
    public string? Notes { get; set; }
    public string? PdfUrl { get; set; }

    /// <summary>Dunning ladder state: D-7 / D-3 / D-day / overdue reminders sent.</summary>
    public int RemindersSent { get; set; }
    public DateTime? LastReminderAtUtc { get; set; }

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class InvoiceLine : BaseEntity
{
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public string Description { get; set; } = string.Empty;
    /// <summary>SAC for services, HSN for retail goods — POS mixes both on one invoice.</summary>
    public string? SacOrHsnCode { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal GstRatePercent { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal LineTotal { get; set; }

    public int? PlanId { get; set; }
    public int? ProductId { get; set; }
}

/// <summary>
/// Append-only payment record — the non-repudiable audit trail required by the NFRs.
/// Partial payments and EMIs are simply multiple rows against one invoice.
/// </summary>
public class Payment : BaseEntity
{
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    public decimal Amount { get; set; }
    public PaymentMode Mode { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Captured;
    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;

    public string? GatewayName { get; set; }
    public string? GatewayOrderId { get; set; }
    public string? GatewayPaymentId { get; set; }
    public string? GatewaySignature { get; set; }
    /// <summary>Raw webhook body kept verbatim for dispute resolution.</summary>
    public string? GatewayPayloadJson { get; set; }

    public string? ChequeNumber { get; set; }
    public string? BankReference { get; set; }
    public string? ReceivedBy { get; set; }
    public string? Notes { get; set; }
    public decimal RefundedAmount { get; set; }
    public string? IdempotencyKey { get; set; }
}

/// <summary>
/// A member's self-serve freeze ask, sitting between the request and the desk's decision.
/// The freeze itself is only ever applied by <c>SubscriptionService.FreezeAsync</c>, so the
/// policy limits (allowance, fee) are enforced in exactly one place regardless of who asked.
/// </summary>
public class FreezeRequest : BaseEntity
{
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public int SubscriptionId { get; set; }
    public Subscription Subscription { get; set; } = null!;

    public DateOnly RequestedFrom { get; set; }
    public DateOnly RequestedTo { get; set; }
    public string Reason { get; set; } = string.Empty;

    public FreezeRequestStatus Status { get; set; } = FreezeRequestStatus.Pending;
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecidedBy { get; set; }
    /// <summary>Shown to the member verbatim — a decline with no reason reads as a system fault.</summary>
    public string? DecisionNote { get; set; }

    public int Days => Math.Max(0, RequestedTo.DayNumber - RequestedFrom.DayNumber);
}
