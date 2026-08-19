using System.Text.Json;
using Gym.Api.Contracts;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers.Portal;

/// <summary>
/// Membership self-serve (Module 3 — Membership): what the member holds, what it is worth,
/// renewing or upgrading it through Razorpay, requesting a freeze, and the invoice history.
///
/// Nothing here re-implements pricing or invoicing. Every sale goes through
/// <see cref="SubscriptionService"/> exactly as an admin sale does, so a member renewing at
/// 11 pm produces the same GST invoice, the same coupon accounting and the same audit trail
/// as the same plan sold at the desk.
/// </summary>
[Route("api/portal")]
public class PortalMembershipController : PortalControllerBase
{
    private readonly GymDbContext _db;
    private readonly SubscriptionService _subscriptions;
    private readonly PaymentOrderService _orders;
    private readonly IPaymentGateway _gateway;
    private readonly INotificationDispatcher _notifier;
    private readonly IClock _clock;
    private readonly ILogger<PortalMembershipController> _log;

    public PortalMembershipController(
        GymDbContext db, SubscriptionService subscriptions, PaymentOrderService orders,
        IPaymentGateway gateway, INotificationDispatcher notifier, IClock clock,
        ILogger<PortalMembershipController> log)
    {
        _db = db;
        _subscriptions = subscriptions;
        _orders = orders;
        _gateway = gateway;
        _notifier = notifier;
        _clock = clock;
        _log = log;
    }

    // ================================================================== read

    [HttpGet("membership")]
    [ProducesResponseType(typeof(PortalMembershipPageResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PortalMembershipPageResponse>> Membership(CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();
        var today = _clock.Today;

        var current = await LoadCurrentAsync(_db, memberId, today, ct);

        var history = await _db.Subscriptions
            .AsNoTracking()
            .Where(s => s.MemberId == memberId)
            .OrderByDescending(s => s.StartsOn)
            .Take(24)
            .Select(s => new
            {
                s.Id, PlanName = s.Plan.Name, s.StartsOn, s.EndsOn, s.Status,
                s.PriceCharged, BranchName = s.Branch.Name
            })
            .ToListAsync(ct);

        var historyRows = history.Select(s => new PortalMembershipHistoryRow
        {
            Id = s.Id,
            PlanName = s.PlanName,
            StartsOn = s.StartsOn.ToString("yyyy-MM-dd"),
            EndsOn = s.EndsOn.ToString("yyyy-MM-dd"),
            StatusName = s.Status.ToString(),
            PriceCharged = s.PriceCharged,
            BranchName = s.BranchName
        }).ToList();

        var invoices = await InvoiceRowsAsync(memberId, 40, ct);
        var dues = invoices.Sum(i => i.AmountDue);

        var freezes = await _db.FreezeRequests
            .AsNoTracking()
            .Include(f => f.Subscription).ThenInclude(s => s.Plan)
            .Where(f => f.MemberId == memberId)
            .OrderByDescending(f => f.RequestedAtUtc)
            .Take(12)
            .ToListAsync(ct);

        return Ok(new PortalMembershipPageResponse
        {
            Current = current,
            History = historyRows,
            Invoices = invoices,
            DuesOutstanding = dues,
            FreezeRequests = freezes.Select(DescribeFreeze).ToList(),
            Gateway = new PortalGatewayInfo
            {
                Provider = _gateway.Name,
                IsLive = _gateway.IsLive,
                KeyId = _gateway.PublicKeyId,
                Notice = _gateway.IsLive
                    ? null
                    : "Payments are running in sandbox simulation while the gym's Razorpay keys are being set up — no money moves."
            }
        });
    }

    /// <summary>
    /// The plans this member can buy, priced at the branch they would buy them at. The list is
    /// the same catalogue the public pricing page reads, so the member is never shown a number
    /// that differs from the one that sold them on it.
    /// </summary>
    [HttpGet("membership/plans")]
    [ProducesResponseType(typeof(IReadOnlyList<PortalPlanOption>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PortalPlanOption>>> Plans(
        [FromQuery] int? branchId, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var member = await _db.Members.AsNoTracking().FirstAsync(m => m.Id == memberId, ct);
        var branch = branchId ?? member.HomeBranchId;

        var currentPlanId = await _db.Subscriptions
            .Where(s => s.MemberId == memberId && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.EndsOn)
            .Select(s => (int?)s.PlanId)
            .FirstOrDefaultAsync(ct);

        var plans = await _db.Plans
            .AsNoTracking()
            .Where(p => p.IsActive && p.ShowOnWebsite)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(ct);

        var overrides = await _db.PlanBranchPrices
            .AsNoTracking()
            .Where(p => p.BranchId == branch && p.IsAvailable)
            .ToDictionaryAsync(p => p.PlanId, ct);

        return Ok(plans.Select(p =>
        {
            var over = overrides.GetValueOrDefault(p.Id);
            return new PortalPlanOption
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Tagline = p.Tagline,
                Kind = p.Kind,
                CycleName = p.Cycle == BillingCycle.None ? "One-off" : Spaced(p.Cycle.ToString()),
                DurationDays = p.DurationDays,
                Price = over?.Price ?? p.BasePrice,
                ListPrice = p.BasePrice,
                AdmissionFee = over?.AdmissionFee ?? p.AdmissionFee,
                AccessScopeName = p.AccessScope == AccessScope.AllBranches ? "All branches" : "Home branch",
                IsMostPopular = p.IsMostPopular,
                IsCurrentPlan = currentPlanId == p.Id,
                ClassCredits = p.ClassCredits,
                PtSessionCredits = p.PtSessionCredits,
                FreezeDaysAllowed = p.FreezeDaysAllowed,
                TrustMicrocopy = p.TrustMicrocopy,
                AccessWindow = p.AccessWindowStart is { } from && p.AccessWindowEnd is { } to
                    ? $"{from:HH\\:mm}–{to:HH\\:mm}"
                    : null,
                Features = ReadFeatures(p.FeaturesJson)
            };
        }).ToList());
    }

    /// <summary>What the member would actually be charged, before they commit to anything.</summary>
    [HttpGet("membership/quote")]
    [ProducesResponseType(typeof(PortalQuoteResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PortalQuoteResponse>> Quote(
        [FromQuery] int planId, [FromQuery] int? branchId, [FromQuery] string? couponCode,
        [FromQuery] bool upgradeNow = false, CancellationToken ct = default)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var plan = await _db.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null) return NotFound();

        var member = await _db.Members.AsNoTracking().Include(m => m.HomeBranch).FirstAsync(m => m.Id == memberId, ct);
        var branch = branchId is { } b
            ? await _db.Branches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == b && x.IsActive, ct)
            : member.HomeBranch;
        if (branch is null) return NotFound();

        var current = await CurrentSubscriptionAsync(memberId, ct);
        var (startsOn, upgradeFrom) = ResolveStart(current, upgradeNow, _clock.Today);

        var quote = await _subscriptions.QuoteAsync(
            memberId, planId, branch.Id, startsOn, couponCode, upgradeFrom, ct);

        return Ok(new PortalQuoteResponse
        {
            PlanId = plan.Id,
            PlanName = plan.Name,
            BranchId = branch.Id,
            BranchName = branch.Name,
            ListPrice = quote.ListPrice,
            AdmissionFee = quote.AdmissionFee,
            DiscountAmount = quote.DiscountAmount,
            ProrationCredit = quote.ProrationCredit,
            Payable = quote.Payable,
            GstRatePercent = plan.GstRatePercent,
            CouponCode = quote.CouponCode,
            CouponMessage = quote.CouponMessage,
            StartsOn = quote.StartsOn.ToString("yyyy-MM-dd"),
            EndsOn = quote.EndsOn.ToString("yyyy-MM-dd"),
            IsRenewalOfCurrent = current is not null && current.PlanId == plan.Id && !upgradeNow
        });
    }

    // ================================================================== write

    /// <summary>
    /// Renew, upgrade or buy a first plan. A straight renewal starts the day the current one
    /// ends — the member keeps every paid day rather than losing the tail by renewing early.
    /// </summary>
    [HttpPost("membership/renew")]
    [ProducesResponseType(typeof(PortalCheckoutResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PortalCheckoutResponse>> Renew(PortalRenewRequest request, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var plan = await _db.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PlanId && p.IsActive, ct);
        if (plan is null) return NotFound();
        if (!plan.ShowOnWebsite)
            return Problem("That plan is only sold at the desk.", statusCode: StatusCodes.Status403Forbidden);

        var member = await _db.Members.AsNoTracking().Include(m => m.HomeBranch).FirstAsync(m => m.Id == memberId, ct);
        var branchId = request.BranchId ?? member.HomeBranchId;
        if (!await _db.Branches.AnyAsync(b => b.Id == branchId && b.IsActive, ct)) return NotFound();

        var current = await CurrentSubscriptionAsync(memberId, ct);
        if (current is { Status: SubscriptionStatus.Frozen })
            return Problem(
                "Resume your frozen membership before buying another. The desk can do it in one step.",
                statusCode: StatusCodes.Status409Conflict);

        var (startsOn, upgradeFrom) = ResolveStart(current, request.UpgradeNow, _clock.Today);

        SaleResult sale;
        try
        {
            sale = await _subscriptions.SellAsync(
                memberId, plan.Id, branchId, startsOn, request.CouponCode, upgradeFrom,
                request.AutoRenew, dueInDays: 0,
                notes: request.UpgradeNow ? "Upgraded in the member app" : "Bought in the member app",
                actor: member.MemberCode, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        PortalOrderResponse? order = null;
        string? gatewayNotice = null;
        if (sale.Invoice.AmountDue > 0)
        {
            try
            {
                var opened = await _orders.OpenAsync(sale.Invoice.Id, null, member.MemberCode, ct);
                order = new PortalOrderResponse
                {
                    OrderId = opened.Order.OrderId,
                    KeyId = opened.Order.KeyId,
                    AmountInr = opened.Order.AmountInr,
                    Currency = opened.Order.Currency,
                    IsSimulated = opened.Order.IsSimulated,
                    PrefillName = member.FullName,
                    PrefillEmail = member.Email,
                    PrefillContact = member.Phone
                };
            }
            catch (InvalidOperationException ex)
            {
                // The membership and its invoice are already real; the member can pay at the
                // desk. Losing the sale because the gateway is unreachable would be worse.
                gatewayNotice = ex.Message;
                _log.LogWarning(ex, "Could not open a gateway order for invoice {Invoice}", sale.Invoice.InvoiceNumber);
            }
        }

        _log.LogInformation("{Member} bought {Plan} in the portal — invoice {Invoice}",
            member.MemberCode, plan.Name, sale.Invoice.InvoiceNumber);

        return Ok(new PortalCheckoutResponse
        {
            InvoiceId = sale.Invoice.Id,
            InvoiceNumber = sale.Invoice.InvoiceNumber,
            AmountDue = sale.Invoice.AmountDue,
            SubscriptionId = sale.Subscription.Id,
            StartsOn = sale.Subscription.StartsOn.ToString("yyyy-MM-dd"),
            EndsOn = sale.Subscription.EndsOn.ToString("yyyy-MM-dd"),
            Order = order,
            Headline = request.UpgradeNow ? "Upgrade ready" : "Nearly there",
            Message = gatewayNotice
                ?? (sale.Invoice.AmountDue > 0
                    ? $"₹{sale.Invoice.AmountDue:N0} to pay. Your plan runs to {sale.Subscription.EndsOn:dd MMM yyyy}."
                    : $"Nothing to pay. Your plan runs to {sale.Subscription.EndsOn:dd MMM yyyy}.")
        });
    }

    /// <summary>Opens a gateway order against an existing unpaid invoice ("pay now").</summary>
    [HttpPost("invoices/{id:int}/pay")]
    [ProducesResponseType(typeof(PortalOrderResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PortalOrderResponse>> Pay(int id, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var invoice = await _db.Invoices.Include(i => i.Member)
            .FirstOrDefaultAsync(i => i.Id == id && i.MemberId == memberId, ct);
        if (invoice is null) return NotFound();

        try
        {
            var opened = await _orders.OpenAsync(invoice.Id, null, invoice.Member.MemberCode, ct);
            return Ok(new PortalOrderResponse
            {
                OrderId = opened.Order.OrderId,
                KeyId = opened.Order.KeyId,
                AmountInr = opened.Order.AmountInr,
                Currency = opened.Order.Currency,
                IsSimulated = opened.Order.IsSimulated,
                PrefillName = invoice.Member.FullName,
                PrefillEmail = invoice.Member.Email,
                PrefillContact = invoice.Member.Phone
            });
        }
        catch (InvalidOperationException ex)
        {
            var status = ex.Message.Contains("outstanding", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status503ServiceUnavailable;
            return Problem(ex.Message, statusCode: status);
        }
    }

    /// <summary>
    /// Asks the desk to freeze the membership. It is a request rather than an action because
    /// the freeze moves the end date and can carry a fee — the gym has to own that decision.
    /// The plan's allowance is checked here anyway, so an impossible ask is refused at the
    /// point the member makes it rather than a day later by a phone call.
    /// </summary>
    [HttpPost("membership/freeze")]
    [ProducesResponseType(typeof(PortalFreezeRequestRow), StatusCodes.Status201Created)]
    public async Task<ActionResult<PortalFreezeRequestRow>> RequestFreeze(
        PortalFreezeRequest request, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var subscription = await _db.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == request.SubscriptionId && s.MemberId == memberId, ct);
        if (subscription is null) return NotFound();

        if (subscription.Status != SubscriptionStatus.Active)
            return Problem("Only an active membership can be frozen.", statusCode: StatusCodes.Status400BadRequest);

        var today = _clock.Today;
        if (request.To <= request.From)
            return Problem("The freeze has to end after it starts.", statusCode: StatusCodes.Status400BadRequest);
        if (request.From < today)
            return Problem("A freeze cannot start in the past.", statusCode: StatusCodes.Status400BadRequest);
        if (request.From > subscription.EndsOn)
            return Problem("That is after your membership already ends.", statusCode: StatusCodes.Status400BadRequest);
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Problem("Tell the desk why — it is what they act on.", statusCode: StatusCodes.Status400BadRequest);

        var days = request.To.DayNumber - request.From.DayNumber;
        var allowance = subscription.Plan.FreezeDaysAllowed;
        if (allowance == 0)
            return Problem($"{subscription.Plan.Name} does not include freeze days.",
                statusCode: StatusCodes.Status400BadRequest);
        if (subscription.FreezeDaysUsed + days > allowance)
            return Problem(
                $"{subscription.Plan.Name} allows {allowance} freeze days and you have used {subscription.FreezeDaysUsed}. " +
                $"That leaves {Math.Max(0, allowance - subscription.FreezeDaysUsed)}.",
                statusCode: StatusCodes.Status400BadRequest);

        var pending = await _db.FreezeRequests
            .AnyAsync(f => f.MemberId == memberId && f.Status == FreezeRequestStatus.Pending, ct);
        if (pending)
            return Conflict(new ProblemDetails
            {
                Title = "One at a time",
                Detail = "You already have a freeze request waiting on the desk.",
                Status = StatusCodes.Status409Conflict
            });

        var row = new FreezeRequest
        {
            MemberId = memberId,
            SubscriptionId = subscription.Id,
            RequestedFrom = request.From,
            RequestedTo = request.To,
            Reason = request.Reason.Trim(),
            Status = FreezeRequestStatus.Pending,
            RequestedAtUtc = _clock.UtcNow
        };
        _db.FreezeRequests.Add(row);
        await _db.SaveChangesAsync(ct);

        var member = await _db.Members.AsNoTracking().FirstAsync(m => m.Id == memberId, ct);
        await _notifier.SendAsync(new OutboundMessage
        {
            Kind = NotificationKind.General,
            Title = "Freeze requested",
            Body = $"{member.FullName} ({member.MemberCode}) asked to freeze {subscription.Plan.Name} for " +
                   $"{days} days from {request.From:dd MMM yyyy}. Reason: {row.Reason}",
            ActionUrl = $"/admin/members/{memberId}",
            TemplateKey = "freeze.requested",
            PayloadJson = JsonSerializer.Serialize(new { freezeRequestId = row.Id, subscriptionId = subscription.Id, days })
        }, ct);

        await _db.Entry(row).Reference(r => r.Subscription).LoadAsync(ct);
        await _db.Entry(row.Subscription).Reference(s => s.Plan).LoadAsync(ct);

        return Created($"/api/portal/membership/freeze/{row.Id}", DescribeFreeze(row));
    }

    /// <summary>Withdraws a request the desk has not acted on yet — plans change.</summary>
    [HttpDelete("membership/freeze/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> WithdrawFreeze(int id, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var row = await _db.FreezeRequests.FirstOrDefaultAsync(f => f.Id == id && f.MemberId == memberId, ct);
        if (row is null) return NotFound();
        if (row.Status != FreezeRequestStatus.Pending)
            return Problem("The desk has already answered that request.", statusCode: StatusCodes.Status400BadRequest);

        row.Status = FreezeRequestStatus.Withdrawn;
        row.DecidedAtUtc = _clock.UtcNow;
        row.DecidedBy = "Member";
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ================================================================== invoices

    [HttpGet("invoices")]
    [ProducesResponseType(typeof(IReadOnlyList<PortalInvoiceRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PortalInvoiceRow>>> Invoices(CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();
        return Ok(await InvoiceRowsAsync(memberId, 60, ct));
    }

    [HttpGet("invoices/{id:int}")]
    [ProducesResponseType(typeof(PortalInvoiceDetail), StatusCodes.Status200OK)]
    public async Task<ActionResult<PortalInvoiceDetail>> Invoice(int id, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Branch)
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == id && i.MemberId == memberId, ct);
        if (invoice is null) return NotFound();

        return Ok(new PortalInvoiceDetail
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            IssuedOn = invoice.IssuedOn.ToString("yyyy-MM-dd"),
            DueOn = invoice.DueOn.ToString("yyyy-MM-dd"),
            Status = invoice.Status,
            StatusName = Spaced(invoice.Status.ToString()),
            GrandTotal = invoice.GrandTotal,
            AmountPaid = invoice.AmountPaid,
            AmountDue = invoice.AmountDue,
            Description = invoice.Lines.FirstOrDefault()?.Description,
            BranchName = invoice.Branch.Name,
            SupplierGstin = invoice.SupplierGstin,
            PlaceOfSupply = invoice.PlaceOfSupply,
            SubTotal = invoice.SubTotal,
            DiscountTotal = invoice.DiscountTotal,
            TaxableValue = invoice.TaxableValue,
            CgstAmount = invoice.CgstAmount,
            SgstAmount = invoice.SgstAmount,
            IgstAmount = invoice.IgstAmount,
            RoundOff = invoice.RoundOff,
            Lines = invoice.Lines.Select(l => new PortalInvoiceLineRow
            {
                Description = l.Description,
                SacOrHsnCode = l.SacOrHsnCode,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                DiscountAmount = l.DiscountAmount,
                TaxableValue = l.TaxableValue,
                GstRatePercent = l.GstRatePercent,
                LineTotal = l.LineTotal
            }).ToList(),
            Payments = invoice.Payments
                .Where(p => p.Status != PaymentStatus.Pending)
                .OrderByDescending(p => p.PaidAtUtc)
                .Select(p => new PortalPaymentRow
                {
                    Id = p.Id,
                    Amount = p.Amount,
                    ModeName = Spaced(p.Mode.ToString()),
                    StatusName = p.Status.ToString(),
                    PaidAtUtc = p.PaidAtUtc,
                    Reference = p.GatewayPaymentId ?? p.BankReference ?? p.ChequeNumber
                }).ToList()
        });
    }

    // ================================================================== helpers

    private async Task<Subscription?> CurrentSubscriptionAsync(int memberId, CancellationToken ct) =>
        await _db.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.MemberId == memberId)
            .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Frozen)
            .OrderByDescending(s => s.EndsOn)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// A renewal picks up where the current plan ends; an upgrade starts today and hands back
    /// the unused days as a proration credit on the invoice.
    /// </summary>
    private static (DateOnly StartsOn, int? UpgradeFrom) ResolveStart(
        Subscription? current, bool upgradeNow, DateOnly today)
    {
        if (current is null) return (today, null);
        if (upgradeNow) return (today, current.Id);
        return (current.EndsOn > today ? current.EndsOn : today, null);
    }

    private async Task<List<PortalInvoiceRow>> InvoiceRowsAsync(int memberId, int take, CancellationToken ct)
    {
        var rows = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.MemberId == memberId && i.Status != InvoiceStatus.Draft)
            .OrderByDescending(i => i.IssuedOn).ThenByDescending(i => i.Id)
            .Take(take)
            .Select(i => new
            {
                i.Id, i.InvoiceNumber, i.IssuedOn, i.DueOn, i.Status,
                i.GrandTotal, i.AmountPaid, i.AmountDue,
                Description = i.Lines.Select(l => l.Description).FirstOrDefault()
            })
            .ToListAsync(ct);

        return rows.Select(i => new PortalInvoiceRow
        {
            Id = i.Id,
            InvoiceNumber = i.InvoiceNumber,
            IssuedOn = i.IssuedOn.ToString("yyyy-MM-dd"),
            DueOn = i.DueOn.ToString("yyyy-MM-dd"),
            Status = i.Status,
            StatusName = Spaced(i.Status.ToString()),
            GrandTotal = i.GrandTotal,
            AmountPaid = i.AmountPaid,
            AmountDue = i.AmountDue,
            Description = i.Description
        }).ToList();
    }

    internal static PortalFreezeRequestRow DescribeFreeze(FreezeRequest f) => new()
    {
        Id = f.Id,
        SubscriptionId = f.SubscriptionId,
        PlanName = f.Subscription?.Plan?.Name ?? "Membership",
        RequestedFrom = f.RequestedFrom.ToString("yyyy-MM-dd"),
        RequestedTo = f.RequestedTo.ToString("yyyy-MM-dd"),
        Days = f.Days,
        Reason = f.Reason,
        Status = f.Status,
        StatusName = f.Status.ToString(),
        RequestedAtUtc = f.RequestedAtUtc,
        DecidedAtUtc = f.DecidedAtUtc,
        DecisionNote = f.DecisionNote,
        MemberName = f.Member?.FullName,
        MemberCode = f.Member?.MemberCode
    };

    /// <summary>
    /// The current membership as the portal renders it. Shared with the home screen so the
    /// "18 days left" on the dashboard and on the membership page can never disagree.
    /// </summary>
    internal static async Task<PortalMembershipResponse?> LoadCurrentAsync(
        GymDbContext db, int memberId, DateOnly today, CancellationToken ct)
    {
        var s = await db.Subscriptions
            .AsNoTracking()
            .Include(x => x.Plan)
            .Include(x => x.Branch)
            .Where(x => x.MemberId == memberId)
            .Where(x => x.Status == SubscriptionStatus.Active || x.Status == SubscriptionStatus.Frozen)
            .OrderByDescending(x => x.EndsOn)
            .FirstOrDefaultAsync(ct);
        if (s is null) return null;

        var pending = await db.FreezeRequests
            .AsNoTracking()
            .Include(f => f.Subscription).ThenInclude(x => x.Plan)
            .Where(f => f.MemberId == memberId && f.Status == FreezeRequestStatus.Pending)
            .OrderByDescending(f => f.RequestedAtUtc)
            .FirstOrDefaultAsync(ct);

        return new PortalMembershipResponse
        {
            SubscriptionId = s.Id,
            PlanName = s.Plan.Name,
            PlanSlug = s.Plan.Slug,
            PlanTagline = s.Plan.Tagline,
            Kind = s.Plan.Kind,
            CycleName = s.Plan.Cycle == BillingCycle.None
                ? "One-off"
                : System.Text.RegularExpressions.Regex.Replace(s.Plan.Cycle.ToString(), "(?<!^)([A-Z])", " $1"),
            Status = s.Status,
            StatusName = s.Status.ToString(),
            BranchName = s.Branch.Name,
            AccessScopeName = s.Plan.AccessScope == AccessScope.AllBranches ? "All branches" : "Home branch",
            StartsOn = s.StartsOn.ToString("yyyy-MM-dd"),
            EndsOn = s.EndsOn.ToString("yyyy-MM-dd"),
            DaysLeft = Math.Max(0, s.EndsOn.DayNumber - today.DayNumber),
            TotalDays = Math.Max(1, s.EndsOn.DayNumber - s.StartsOn.DayNumber),
            ClassCreditsRemaining = s.ClassCreditsRemaining,
            PtCreditsRemaining = s.PtCreditsRemaining,
            PriceCharged = s.PriceCharged,
            AutoRenew = s.AutoRenew,
            NextBillingOn = s.NextBillingOn?.ToString("yyyy-MM-dd"),
            FreezeDaysAllowed = s.Plan.FreezeDaysAllowed,
            FreezeDaysUsed = s.FreezeDaysUsed,
            FreezeFee = s.Plan.FreezeFee,
            FreezeStartsOn = s.FreezeStartsOn?.ToString("yyyy-MM-dd"),
            FreezeEndsOn = s.FreezeEndsOn?.ToString("yyyy-MM-dd"),
            AccessWindow = s.Plan.AccessWindowStart is { } from && s.Plan.AccessWindowEnd is { } to
                ? $"{from:HH\\:mm}–{to:HH\\:mm}"
                : null,
            PendingFreezeRequest = pending is null ? null : DescribeFreeze(pending),
            Features = ReadFeatures(s.Plan.FeaturesJson)
        };
    }

    /// <summary>A malformed feature list costs the member the whole page otherwise.</summary>
    private static IReadOnlyList<string> ReadFeatures(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
