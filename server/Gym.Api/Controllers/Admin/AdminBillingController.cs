using System.Text.Json;
using Gym.Api.Contracts;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.BackgroundJobs;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers.Admin;

/// <summary>
/// Memberships and billing: the plan catalogue with per-branch pricing, coupons, selling and
/// servicing subscriptions, and the GST invoice ledger with its payments and collections view.
/// Money only ever moves through <see cref="InvoiceService"/> and <see cref="SubscriptionService"/>,
/// so the audit trail is identical whichever screen started the transaction.
/// </summary>
[ApiController]
[Route("api/admin/billing")]
[Authorize(Roles = RoleNames.Admin)]
[Produces("application/json")]
public class AdminBillingController : ControllerBase
{
    private readonly GymDbContext _db;
    private readonly InvoiceService _invoices;
    private readonly SubscriptionService _subscriptions;
    private readonly INotificationDispatcher _notifier;
    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private readonly ILogger<AdminBillingController> _log;

    public AdminBillingController(
        GymDbContext db, InvoiceService invoices, SubscriptionService subscriptions,
        INotificationDispatcher notifier, IServiceScopeFactory scopes, IClock clock,
        ILogger<AdminBillingController> log)
    {
        _db = db;
        _invoices = invoices;
        _subscriptions = subscriptions;
        _notifier = notifier;
        _scopes = scopes;
        _clock = clock;
        _log = log;
    }

    // ================================================================== plans

    [HttpGet("plans")]
    [ProducesResponseType(typeof(IReadOnlyList<PlanRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PlanRow>>> Plans(CancellationToken ct)
    {
        var plans = await _db.Plans.AsNoTracking()
            .Include(p => p.BranchPrices).ThenInclude(bp => bp.Branch)
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name)
            .ToListAsync(ct);

        var counts = await _db.Subscriptions.AsNoTracking()
            .Where(s => s.Status == SubscriptionStatus.Active)
            .GroupBy(s => s.PlanId)
            .Select(g => new { PlanId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PlanId, x => x.Count, ct);

        return Ok(plans.Select(p => Describe(p, counts.GetValueOrDefault(p.Id))).ToList());
    }

    [HttpPost("plans")]
    [ProducesResponseType(typeof(PlanRow), StatusCodes.Status201Created)]
    public async Task<ActionResult<PlanRow>> CreatePlan(UpsertPlanRequest request, CancellationToken ct)
    {
        var slug = AdminCmsController.Slugify(request.Slug);
        if (await _db.Plans.AnyAsync(p => p.Slug == slug, ct))
        {
            ModelState.AddModelError(nameof(request.Slug), "A plan already uses that slug.");
            return ValidationProblem(ModelState);
        }

        var plan = new Plan { Slug = slug, CreatedBy = User.Identity?.Name };
        Apply(plan, request);
        _db.Plans.Add(plan);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Plans), Describe(plan, 0));
    }

    [HttpPut("plans/{id:int}")]
    [ProducesResponseType(typeof(PlanRow), StatusCodes.Status200OK)]
    public async Task<ActionResult<PlanRow>> UpdatePlan(int id, UpsertPlanRequest request, CancellationToken ct)
    {
        var plan = await _db.Plans.Include(p => p.BranchPrices).ThenInclude(bp => bp.Branch)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return NotFound();

        var slug = AdminCmsController.Slugify(request.Slug);
        if (slug != plan.Slug && await _db.Plans.AnyAsync(p => p.Slug == slug && p.Id != id, ct))
        {
            ModelState.AddModelError(nameof(request.Slug), "A plan already uses that slug.");
            return ValidationProblem(ModelState);
        }

        plan.Slug = slug;
        Apply(plan, request);
        plan.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync(ct);

        var count = await _db.Subscriptions.CountAsync(s => s.PlanId == id && s.Status == SubscriptionStatus.Active, ct);
        return Ok(Describe(plan, count));
    }

    /// <summary>
    /// Retires a plan rather than deleting it when subscriptions exist — the invoices that
    /// reference it must keep resolving, so history stays readable years later.
    /// </summary>
    [HttpDelete("plans/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeletePlan(int id, CancellationToken ct)
    {
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return NotFound();

        var inUse = await _db.Subscriptions.AnyAsync(s => s.PlanId == id, ct);
        if (inUse)
        {
            plan.IsActive = false;
            plan.ShowOnWebsite = false;
            await _db.SaveChangesAsync(ct);
            return Ok(new { retired = true, message = "The plan has subscriptions, so it was retired rather than deleted." });
        }

        _db.Plans.Remove(plan);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("plans/{id:int}/prices")]
    [ProducesResponseType(typeof(PlanRow), StatusCodes.Status200OK)]
    public async Task<ActionResult<PlanRow>> SetBranchPrices(
        int id, SetBranchPricesRequest request, CancellationToken ct)
    {
        var plan = await _db.Plans.Include(p => p.BranchPrices).ThenInclude(bp => bp.Branch)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return NotFound();

        foreach (var input in request.Prices)
        {
            var row = plan.BranchPrices.FirstOrDefault(bp => bp.BranchId == input.BranchId);
            if (row is null)
            {
                row = new PlanBranchPrice { PlanId = id, BranchId = input.BranchId };
                plan.BranchPrices.Add(row);
            }
            row.Price = input.Price;
            row.AdmissionFee = input.AdmissionFee;
            row.IsAvailable = input.IsAvailable;
        }

        // A branch dropped from the payload has no override any more and falls back to list.
        var keep = request.Prices.Select(p => p.BranchId).ToHashSet();
        foreach (var stale in plan.BranchPrices.Where(bp => !keep.Contains(bp.BranchId)).ToList())
            _db.PlanBranchPrices.Remove(stale);

        await _db.SaveChangesAsync(ct);

        var refreshed = await _db.Plans.AsNoTracking()
            .Include(p => p.BranchPrices).ThenInclude(bp => bp.Branch)
            .FirstAsync(p => p.Id == id, ct);
        var count = await _db.Subscriptions.CountAsync(s => s.PlanId == id && s.Status == SubscriptionStatus.Active, ct);
        return Ok(Describe(refreshed, count));
    }

    // ================================================================== coupons

    [HttpGet("coupons")]
    [ProducesResponseType(typeof(IReadOnlyList<CouponRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CouponRow>>> Coupons(CancellationToken ct)
    {
        var today = _clock.Today;
        var coupons = await _db.Coupons.AsNoTracking().OrderByDescending(c => c.Id).ToListAsync(ct);
        return Ok(coupons.Select(c => Describe(c, today)).ToList());
    }

    [HttpPost("coupons")]
    [ProducesResponseType(typeof(CouponRow), StatusCodes.Status201Created)]
    public async Task<ActionResult<CouponRow>> CreateCoupon(UpsertCouponRequest request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await _db.Coupons.AnyAsync(c => c.Code == code, ct))
        {
            ModelState.AddModelError(nameof(request.Code), "That code already exists.");
            return ValidationProblem(ModelState);
        }

        var coupon = new Coupon { Code = code, CreatedBy = User.Identity?.Name };
        Apply(coupon, request);
        _db.Coupons.Add(coupon);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Coupons), Describe(coupon, _clock.Today));
    }

    [HttpPut("coupons/{id:int}")]
    [ProducesResponseType(typeof(CouponRow), StatusCodes.Status200OK)]
    public async Task<ActionResult<CouponRow>> UpdateCoupon(int id, UpsertCouponRequest request, CancellationToken ct)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (coupon is null) return NotFound();

        var code = request.Code.Trim().ToUpperInvariant();
        if (code != coupon.Code && await _db.Coupons.AnyAsync(c => c.Code == code && c.Id != id, ct))
        {
            ModelState.AddModelError(nameof(request.Code), "That code already exists.");
            return ValidationProblem(ModelState);
        }

        coupon.Code = code;
        Apply(coupon, request);
        coupon.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync(ct);
        return Ok(Describe(coupon, _clock.Today));
    }

    [HttpDelete("coupons/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteCoupon(int id, CancellationToken ct)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (coupon is null) return NotFound();

        if (await _db.Subscriptions.AnyAsync(s => s.CouponId == id, ct))
        {
            coupon.IsActive = false;
            coupon.ShowAsWebsiteBanner = false;
            await _db.SaveChangesAsync(ct);
            return Ok(new { retired = true, message = "The coupon has been redeemed, so it was switched off rather than deleted." });
        }

        _db.Coupons.Remove(coupon);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ================================================================== subscriptions

    [HttpGet("quote")]
    [ProducesResponseType(typeof(PriceQuote), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> Quote(
        [FromQuery] int memberId, [FromQuery] int planId, [FromQuery] int branchId,
        [FromQuery] DateOnly? startsOn, [FromQuery] string? couponCode,
        [FromQuery] int? upgradeFromSubscriptionId, CancellationToken ct = default)
    {
        if (!await _db.Members.AnyAsync(m => m.Id == memberId, ct)) return NotFound();
        if (!await _db.Plans.AnyAsync(p => p.Id == planId, ct)) return NotFound();

        var quote = await _subscriptions.QuoteAsync(
            memberId, planId, branchId, startsOn ?? _clock.Today, couponCode, upgradeFromSubscriptionId, ct);

        // Show the tax split at quote time so the desk can answer "what's the GST on that?".
        var plan = await _db.Plans.AsNoTracking().FirstAsync(p => p.Id == planId, ct);
        var split = GstCalculator.FromGross(quote.Payable, plan.GstRatePercent);

        return Ok(new
        {
            quote.ListPrice, quote.AdmissionFee, quote.DiscountAmount, quote.ProrationCredit,
            quote.Payable, quote.CouponId, quote.CouponCode, quote.CouponMessage,
            StartsOn = quote.StartsOn.ToString("yyyy-MM-dd"),
            EndsOn = quote.EndsOn.ToString("yyyy-MM-dd"),
            Tax = new { split.TaxableValue, split.Cgst, split.Sgst, split.Igst, Rate = plan.GstRatePercent }
        });
    }

    [HttpPost("subscriptions")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    public async Task<IActionResult> Sell(SellPlanRequest request, CancellationToken ct)
    {
        if (!await _db.Members.AnyAsync(m => m.Id == request.MemberId, ct))
        {
            ModelState.AddModelError(nameof(request.MemberId), "No such member.");
            return ValidationProblem(ModelState);
        }

        SaleResult sale;
        try
        {
            sale = await _subscriptions.SellAsync(
                request.MemberId, request.PlanId, request.BranchId, request.StartsOn ?? _clock.Today,
                request.CouponCode, request.UpgradeFromSubscriptionId, request.AutoRenew,
                request.DueInDays, request.Notes, User.Identity?.Name, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        if (request.CollectMode is { } mode)
        {
            var amount = request.CollectAmount ?? sale.Invoice.GrandTotal;
            await _invoices.RecordPaymentAsync(
                sale.Invoice, amount, mode, User.Identity?.Name,
                idempotencyKey: $"sale-{sale.Invoice.Id}-{amount:0.00}",
                notes: "Collected at the point of sale", ct: ct);
            await _db.SaveChangesAsync(ct);
        }

        await _notifier.SendAsync(new OutboundMessage
        {
            MemberId = request.MemberId,
            Kind = NotificationKind.PaymentReceived,
            Title = "Membership confirmed",
            Body = $"Invoice {sale.Invoice.InvoiceNumber} for ₹{sale.Invoice.GrandTotal:N0}. " +
                   $"Valid to {sale.Subscription.EndsOn:dd MMM yyyy}.",
            ActionUrl = $"/portal/billing/{sale.Invoice.Id}",
            Channels = new[] { NotificationChannel.InApp, NotificationChannel.WhatsApp, NotificationChannel.Email }
        }, ct);

        return Created($"/api/admin/billing/invoices/{sale.Invoice.Id}", new
        {
            subscriptionId = sale.Subscription.Id,
            invoiceId = sale.Invoice.Id,
            invoiceNumber = sale.Invoice.InvoiceNumber,
            grandTotal = sale.Invoice.GrandTotal,
            amountDue = sale.Invoice.AmountDue,
            endsOn = sale.Subscription.EndsOn.ToString("yyyy-MM-dd")
        });
    }

    [HttpGet("subscriptions")]
    [ProducesResponseType(typeof(PagedResult<SubscriptionRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SubscriptionRow>>> Subscriptions(
        [FromQuery] int? branchId, [FromQuery] SubscriptionStatus? status, [FromQuery] bool? expiringSoon,
        [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var today = _clock.Today;
        var query = _db.Subscriptions.AsNoTracking();

        if (branchId is not null) query = query.Where(s => s.BranchId == branchId);
        if (status is not null) query = query.Where(s => s.Status == status);
        if (expiringSoon == true)
            query = query.Where(s => s.Status == SubscriptionStatus.Active
                                  && s.EndsOn >= today && s.EndsOn <= today.AddDays(14));
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(s => s.Member.FullName.Contains(term)
                                  || s.Member.MemberCode.Contains(term)
                                  || s.Member.Phone.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var size = Math.Clamp(pageSize, 1, 200);

        var items = await query
            .OrderBy(s => s.Status == SubscriptionStatus.Active ? 0 : 1).ThenBy(s => s.EndsOn)
            .Skip((Math.Max(1, page) - 1) * size)
            .Take(size)
            .Select(s => new SubscriptionRow
            {
                Id = s.Id, MemberId = s.MemberId, MemberName = s.Member.FullName, MemberCode = s.Member.MemberCode,
                PlanId = s.PlanId, PlanName = s.Plan.Name, BranchName = s.Branch.Name, Status = s.Status,
                StartsOn = s.StartsOn.ToString("yyyy-MM-dd"), EndsOn = s.EndsOn.ToString("yyyy-MM-dd"),
                DaysLeft = s.EndsOn.DayNumber - today.DayNumber,
                PriceCharged = s.PriceCharged, DiscountAmount = s.DiscountAmount,
                ClassCreditsRemaining = s.ClassCreditsRemaining, PtCreditsRemaining = s.PtCreditsRemaining,
                FreezeStartsOn = s.FreezeStartsOn != null ? s.FreezeStartsOn.Value.ToString("yyyy-MM-dd") : null,
                FreezeEndsOn = s.FreezeEndsOn != null ? s.FreezeEndsOn.Value.ToString("yyyy-MM-dd") : null,
                FreezeDaysUsed = s.FreezeDaysUsed, FreezeDaysAllowed = s.Plan.FreezeDaysAllowed,
                AutoRenew = s.AutoRenew, CancellationReason = s.CancellationReason
            })
            .ToListAsync(ct);

        return Ok(new PagedResult<SubscriptionRow>
        {
            Items = items, Total = total, Page = Math.Max(1, page), PageSize = size
        });
    }

    [HttpPost("subscriptions/{id:int}/freeze")]
    public async Task<IActionResult> Freeze(int id, FreezeSubscriptionRequest request, CancellationToken ct)
    {
        try
        {
            var subscription = await _subscriptions.FreezeAsync(id, request.From, request.To, User.Identity?.Name, ct);
            return Ok(new
            {
                subscription.Id,
                EndsOn = subscription.EndsOn.ToString("yyyy-MM-dd"),
                subscription.FreezeDaysUsed
            });
        }
        catch (InvalidOperationException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("subscriptions/{id:int}/resume")]
    public async Task<IActionResult> Resume(int id, CancellationToken ct)
    {
        var subscription = await _subscriptions.ResumeAsync(id, User.Identity?.Name, ct);
        return Ok(new { subscription.Id, subscription.Status, EndsOn = subscription.EndsOn.ToString("yyyy-MM-dd") });
    }

    [HttpPost("subscriptions/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromBody] CancelSubscriptionRequest request, CancellationToken ct)
    {
        var subscription = await _subscriptions.CancelAsync(id, request.Reason, User.Identity?.Name, ct);
        return Ok(new { subscription.Id, subscription.Status });
    }

    // ================================================================== freeze requests

    /// <summary>
    /// Freeze asks coming out of the member portal. The desk decides; the freeze itself still
    /// runs through <see cref="SubscriptionService.FreezeAsync"/>, so the plan's allowance and
    /// the end-date arithmetic are the same whether the member asked or the desk just did it.
    /// </summary>
    [HttpGet("freeze-requests")]
    [ProducesResponseType(typeof(IReadOnlyList<PortalFreezeRequestRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PortalFreezeRequestRow>>> FreezeRequests(
        [FromQuery] int? memberId, [FromQuery] bool pendingOnly = true, CancellationToken ct = default)
    {
        var query = _db.FreezeRequests
            .AsNoTracking()
            .Include(f => f.Member)
            .Include(f => f.Subscription).ThenInclude(s => s.Plan)
            .AsQueryable();

        if (memberId is { } id) query = query.Where(f => f.MemberId == id);
        if (pendingOnly) query = query.Where(f => f.Status == FreezeRequestStatus.Pending);

        var rows = await query
            .OrderBy(f => f.Status == FreezeRequestStatus.Pending ? 0 : 1)
            .ThenByDescending(f => f.RequestedAtUtc)
            .Take(200)
            .ToListAsync(ct);

        return Ok(rows.Select(Portal.PortalMembershipController.DescribeFreeze).ToList());
    }

    [HttpPost("freeze-requests/{id:int}/decide")]
    [ProducesResponseType(typeof(PortalFreezeRequestRow), StatusCodes.Status200OK)]
    public async Task<ActionResult<PortalFreezeRequestRow>> DecideFreeze(
        int id, DecideFreezeRequest request, CancellationToken ct)
    {
        var freeze = await _db.FreezeRequests
            .Include(f => f.Member)
            .Include(f => f.Subscription).ThenInclude(s => s.Plan)
            .FirstOrDefaultAsync(f => f.Id == id, ct);
        if (freeze is null) return NotFound();
        if (freeze.Status != FreezeRequestStatus.Pending)
            return Problem("That request has already been answered.", statusCode: StatusCodes.Status400BadRequest);

        var actor = User.Identity?.Name;

        if (request.Approve)
        {
            try
            {
                await _subscriptions.FreezeAsync(
                    freeze.SubscriptionId, freeze.RequestedFrom, freeze.RequestedTo, actor, ct);
            }
            catch (InvalidOperationException ex)
            {
                // The allowance changed since they asked — say so rather than half-applying it.
                return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        }

        freeze.Status = request.Approve ? FreezeRequestStatus.Approved : FreezeRequestStatus.Declined;
        freeze.DecidedAtUtc = _clock.UtcNow;
        freeze.DecidedBy = actor;
        freeze.DecisionNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        await _db.SaveChangesAsync(ct);

        await _notifier.SendAsync(new OutboundMessage
        {
            MemberId = freeze.MemberId,
            Kind = NotificationKind.General,
            Title = request.Approve ? "Your freeze is on" : "Freeze request declined",
            Body = request.Approve
                ? $"Frozen from {freeze.RequestedFrom:dd MMM yyyy} to {freeze.RequestedTo:dd MMM yyyy}. " +
                  "Those days are added to the end of your membership."
                : freeze.DecisionNote ?? "Call the desk and we will work something out.",
            ActionUrl = "/portal/membership",
            TemplateKey = request.Approve ? "freeze.approved" : "freeze.declined",
            Channels = new[] { NotificationChannel.InApp, NotificationChannel.WhatsApp }
        }, ct);

        _log.LogInformation("Freeze request {Id} {Decision} for {Member}",
            freeze.Id, request.Approve ? "approved" : "declined", freeze.Member.MemberCode);

        return Ok(Portal.PortalMembershipController.DescribeFreeze(freeze));
    }

    // ================================================================== invoices

    [HttpGet("invoices")]
    [ProducesResponseType(typeof(PagedResult<InvoiceRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<InvoiceRow>>> Invoices(
        [FromQuery] string? q, [FromQuery] int? branchId, [FromQuery] InvoiceStatus? status,
        [FromQuery] bool? unpaidOnly, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var today = _clock.Today;
        var query = _db.Invoices.AsNoTracking();

        if (branchId is not null) query = query.Where(i => i.BranchId == branchId);
        if (status is not null) query = query.Where(i => i.Status == status);
        if (unpaidOnly == true)
            query = query.Where(i => i.AmountDue > 0 && i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Refunded);
        if (from is not null) query = query.Where(i => i.IssuedOn >= from);
        if (to is not null) query = query.Where(i => i.IssuedOn <= to);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(i => i.InvoiceNumber.Contains(term)
                                  || i.Member.FullName.Contains(term)
                                  || i.Member.MemberCode.Contains(term)
                                  || i.Member.Phone.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var size = Math.Clamp(pageSize, 1, 200);

        var entities = await query
            .Include(i => i.Member)
            .Include(i => i.Branch)
            .Include(i => i.Subscription).ThenInclude(s => s!.Plan)
            .OrderByDescending(i => i.IssuedOn).ThenByDescending(i => i.Id)
            .Skip((Math.Max(1, page) - 1) * size)
            .Take(size)
            .ToListAsync(ct);
        var items = entities.Select(i => Project(i, today)).ToList();

        var summary = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Billed = g.Sum(i => i.GrandTotal),
                Collected = g.Sum(i => i.AmountPaid),
                Outstanding = g.Sum(i => i.AmountDue)
            })
            .FirstOrDefaultAsync(ct);

        Response.Headers["X-Total-Billed"] = (summary?.Billed ?? 0).ToString("0.00");
        Response.Headers["X-Total-Collected"] = (summary?.Collected ?? 0).ToString("0.00");
        Response.Headers["X-Total-Outstanding"] = (summary?.Outstanding ?? 0).ToString("0.00");

        return Ok(new PagedResult<InvoiceRow>
        {
            Items = items, Total = total, Page = Math.Max(1, page), PageSize = size
        });
    }

    [HttpGet("invoices/{id:int}")]
    [ProducesResponseType(typeof(InvoiceDetailResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceDetailResponse>> Invoice(int id, CancellationToken ct)
    {
        var today = _clock.Today;
        var invoice = await _db.Invoices.AsNoTracking()
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .Include(i => i.Member)
            .Include(i => i.Branch)
            .Include(i => i.Subscription).ThenInclude(s => s!.Plan)
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (invoice is null) return NotFound();

        return Ok(new InvoiceDetailResponse
        {
            Header = new InvoiceRow
            {
                Id = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                MemberId = invoice.MemberId,
                MemberName = invoice.Member.FullName,
                MemberCode = invoice.Member.MemberCode,
                BranchName = invoice.Branch.Name,
                IssuedOn = invoice.IssuedOn.ToString("yyyy-MM-dd"),
                DueOn = invoice.DueOn.ToString("yyyy-MM-dd"),
                Status = invoice.Status,
                GrandTotal = invoice.GrandTotal,
                AmountPaid = invoice.AmountPaid,
                AmountDue = invoice.AmountDue,
                RemindersSent = invoice.RemindersSent,
                DaysOverdue = invoice.AmountDue > 0 && invoice.DueOn < today
                    ? today.DayNumber - invoice.DueOn.DayNumber : 0,
                PlanName = invoice.Subscription?.Plan.Name
            },
            Lines = invoice.Lines.Select(l => new InvoiceLineRow
            {
                Id = l.Id, Description = l.Description, SacOrHsnCode = l.SacOrHsnCode,
                Quantity = l.Quantity, UnitPrice = l.UnitPrice, DiscountAmount = l.DiscountAmount,
                TaxableValue = l.TaxableValue, GstRatePercent = l.GstRatePercent,
                CgstAmount = l.CgstAmount, SgstAmount = l.SgstAmount, IgstAmount = l.IgstAmount,
                LineTotal = l.LineTotal
            }).ToList(),
            Payments = invoice.Payments.OrderByDescending(p => p.PaidAtUtc).Select(p => new PaymentRow
            {
                Id = p.Id, Amount = p.Amount, Mode = p.Mode, Status = p.Status, PaidAtUtc = p.PaidAtUtc,
                GatewayName = p.GatewayName, GatewayPaymentId = p.GatewayPaymentId,
                ChequeNumber = p.ChequeNumber, BankReference = p.BankReference,
                ReceivedBy = p.ReceivedBy, Notes = p.Notes
            }).ToList(),
            SubTotal = invoice.SubTotal,
            DiscountTotal = invoice.DiscountTotal,
            TaxableValue = invoice.TaxableValue,
            CgstAmount = invoice.CgstAmount,
            SgstAmount = invoice.SgstAmount,
            IgstAmount = invoice.IgstAmount,
            RoundOff = invoice.RoundOff,
            SupplierGstin = invoice.SupplierGstin,
            PlaceOfSupply = invoice.PlaceOfSupply,
            CustomerGstin = invoice.CustomerGstin,
            Notes = invoice.Notes,
            BranchAddress = string.Join(", ", new[]
            {
                invoice.Branch.AddressLine1, invoice.Branch.AddressLine2,
                invoice.Branch.City, invoice.Branch.Pincode
            }.Where(part => !string.IsNullOrWhiteSpace(part))),
            MemberPhone = invoice.Member.Phone,
            MemberEmail = invoice.Member.Email
        });
    }

    [HttpPost("payments")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    public async Task<IActionResult> RecordPayment(RecordPaymentRequest request, CancellationToken ct)
    {
        var invoice = await _db.Invoices.Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, ct);
        if (invoice is null) return NotFound();

        if (invoice.Status is InvoiceStatus.Cancelled or InvoiceStatus.Refunded)
            return Problem("That invoice is closed.", statusCode: StatusCodes.Status400BadRequest);

        // Overpaying is almost always a typo; refuse rather than create a negative balance.
        if (request.Amount > invoice.AmountDue + 0.01m)
        {
            ModelState.AddModelError(nameof(request.Amount),
                $"That is more than the ₹{invoice.AmountDue:N2} outstanding.");
            return ValidationProblem(ModelState);
        }

        var payment = await _invoices.RecordPaymentAsync(
            invoice, request.Amount, request.Mode, User.Identity?.Name,
            idempotencyKey: request.IdempotencyKey,
            chequeNumber: request.ChequeNumber,
            bankReference: request.BankReference,
            notes: request.Notes, ct: ct);

        await _db.SaveChangesAsync(ct);

        if (invoice.Status == InvoiceStatus.Paid)
            await _notifier.SendAsync(new OutboundMessage
            {
                MemberId = invoice.MemberId,
                Kind = NotificationKind.PaymentReceived,
                Title = "Payment received",
                Body = $"₹{invoice.GrandTotal:N0} against {invoice.InvoiceNumber}. Thank you.",
                ActionUrl = $"/portal/billing/{invoice.Id}",
                Channels = new[] { NotificationChannel.InApp, NotificationChannel.WhatsApp }
            }, ct);

        return Created($"/api/admin/billing/invoices/{invoice.Id}", new
        {
            paymentId = payment.Id,
            invoice.Status,
            invoice.AmountPaid,
            invoice.AmountDue
        });
    }

    [HttpPost("invoices/{id:int}/cancel")]
    public async Task<IActionResult> CancelInvoice(int id, [FromBody] CancelSubscriptionRequest request, CancellationToken ct)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null) return NotFound();

        if (invoice.AmountPaid > 0)
            return Problem("Money has already been received against this invoice; raise a refund instead.",
                statusCode: StatusCodes.Status400BadRequest);

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.Notes = string.Join(" · ", new[] { invoice.Notes, $"Cancelled: {request.Reason}" }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        invoice.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Sends the next dunning message for one invoice, out of band from the sweep.</summary>
    [HttpPost("invoices/{id:int}/remind")]
    public async Task<IActionResult> Remind(int id, CancellationToken ct)
    {
        var invoice = await _db.Invoices.Include(i => i.Member).FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null) return NotFound();
        if (invoice.AmountDue <= 0) return Problem("Nothing is outstanding.", statusCode: 400);

        invoice.RemindersSent += 1;
        invoice.LastReminderAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _notifier.SendAsync(new OutboundMessage
        {
            MemberId = invoice.MemberId,
            Kind = NotificationKind.PaymentDue,
            Title = invoice.DueOn < _clock.Today ? "Payment overdue" : "Payment due",
            Body = $"Invoice {invoice.InvoiceNumber} has ₹{invoice.AmountDue:N0} outstanding, " +
                   $"due {invoice.DueOn:dd MMM yyyy}.",
            ActionUrl = $"/portal/billing/{invoice.Id}",
            TemplateKey = "dunning.manual",
            Channels = new[]
            {
                NotificationChannel.InApp, NotificationChannel.WhatsApp,
                NotificationChannel.Sms, NotificationChannel.Email
            }
        }, ct);

        return Ok(new { invoice.RemindersSent, invoice.LastReminderAtUtc });
    }

    /// <summary>Runs the whole dunning ladder now instead of waiting for the six-hourly sweep.</summary>
    [HttpPost("collections/run")]
    public async Task<IActionResult> RunCollections(CancellationToken ct)
    {
        var sent = await DunningWorker.RunOnceAsync(_scopes, ct);
        _log.LogInformation("Collections run triggered by {User}: {Count} reminder(s)", User.Identity?.Name, sent);
        return Ok(new { remindersSent = sent });
    }

    /// <summary>The collections dashboard: what is owed, by how long, and by whom.</summary>
    [HttpGet("collections")]
    public async Task<IActionResult> Collections([FromQuery] int? branchId, CancellationToken ct)
    {
        var today = _clock.Today;
        var query = _db.Invoices.AsNoTracking()
            .Where(i => i.AmountDue > 0 && i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Refunded);
        if (branchId is not null) query = query.Where(i => i.BranchId == branchId);

        var rows = await query
            .OrderBy(i => i.DueOn)
            .Select(i => new
            {
                i.Id, i.InvoiceNumber, i.MemberId, MemberName = i.Member.FullName,
                MemberCode = i.Member.MemberCode, Phone = i.Member.Phone, BranchName = i.Branch.Name,
                i.DueOn, i.AmountDue, i.GrandTotal, i.RemindersSent, i.LastReminderAtUtc, i.Status
            })
            .Take(500)
            .ToListAsync(ct);

        // Standard ageing buckets — this is how a collections list is read everywhere.
        static string Bucket(int daysOverdue) => daysOverdue switch
        {
            <= 0 => "Not yet due",
            <= 15 => "1–15 days",
            <= 30 => "16–30 days",
            <= 60 => "31–60 days",
            _ => "60+ days"
        };

        var enriched = rows.Select(r => new
        {
            r.Id, r.InvoiceNumber, r.MemberId, r.MemberName, r.MemberCode, r.Phone, r.BranchName,
            DueOn = r.DueOn.ToString("yyyy-MM-dd"),
            r.AmountDue, r.GrandTotal, r.RemindersSent, r.LastReminderAtUtc, r.Status,
            DaysOverdue = Math.Max(0, today.DayNumber - r.DueOn.DayNumber),
            Bucket = Bucket(today.DayNumber - r.DueOn.DayNumber)
        }).ToList();

        return Ok(new
        {
            totalOutstanding = enriched.Sum(r => r.AmountDue),
            invoiceCount = enriched.Count,
            ageing = enriched
                .GroupBy(r => r.Bucket)
                .Select(g => new { bucket = g.Key, amount = g.Sum(r => r.AmountDue), count = g.Count() })
                .ToList(),
            invoices = enriched
        });
    }

    // ------------------------------------------------------------------ mapping

    private static InvoiceRow Project(Invoice i, DateOnly today) => new()
    {
        Id = i.Id,
        InvoiceNumber = i.InvoiceNumber,
        MemberId = i.MemberId,
        MemberName = i.Member.FullName,
        MemberCode = i.Member.MemberCode,
        BranchName = i.Branch.Name,
        IssuedOn = i.IssuedOn.ToString("yyyy-MM-dd"),
        DueOn = i.DueOn.ToString("yyyy-MM-dd"),
        Status = i.Status,
        GrandTotal = i.GrandTotal,
        AmountPaid = i.AmountPaid,
        AmountDue = i.AmountDue,
        RemindersSent = i.RemindersSent,
        DaysOverdue = i.AmountDue > 0 && i.DueOn < today ? today.DayNumber - i.DueOn.DayNumber : 0,
        PlanName = i.Subscription != null ? i.Subscription.Plan.Name : null
    };

    private static void Apply(Plan plan, UpsertPlanRequest r)
    {
        plan.Name = r.Name.Trim();
        plan.Tagline = r.Tagline;
        plan.Description = r.Description;
        plan.Kind = r.Kind;
        plan.Cycle = r.Cycle;
        plan.AccessScope = r.AccessScope;
        plan.DurationDays = r.DurationDays;
        plan.BasePrice = r.BasePrice;
        plan.AdmissionFee = r.AdmissionFee;
        plan.GstRatePercent = r.GstRatePercent;
        plan.SacCode = r.SacCode;
        plan.ClassCredits = r.ClassCredits;
        plan.PtSessionCredits = r.PtSessionCredits;
        plan.GuestPasses = r.GuestPasses;
        plan.FreezeDaysAllowed = r.FreezeDaysAllowed;
        plan.FreezeFee = r.FreezeFee;
        plan.AccessWindowStart = r.AccessWindowStart;
        plan.AccessWindowEnd = r.AccessWindowEnd;
        plan.FeaturesJson = JsonSerializer.Serialize(r.Features);
        plan.TrustMicrocopy = r.TrustMicrocopy;
        plan.IsMostPopular = r.IsMostPopular;
        plan.ShowOnWebsite = r.ShowOnWebsite;
        plan.IsActive = r.IsActive;
        plan.DisplayOrder = r.DisplayOrder;
    }

    private static void Apply(Coupon coupon, UpsertCouponRequest r)
    {
        coupon.Name = r.Name.Trim();
        coupon.Description = r.Description;
        coupon.DiscountType = r.DiscountType;
        coupon.DiscountValue = r.DiscountValue;
        coupon.MaxDiscountAmount = r.MaxDiscountAmount;
        coupon.MinOrderAmount = r.MinOrderAmount;
        coupon.ValidFrom = r.ValidFrom;
        coupon.ValidTo = r.ValidTo;
        coupon.UsageCap = r.UsageCap;
        coupon.PerMemberCap = r.PerMemberCap;
        coupon.BranchScope = r.BranchScope;
        coupon.PlanScope = r.PlanScope;
        coupon.IsActive = r.IsActive;
        coupon.ShowAsWebsiteBanner = r.ShowAsWebsiteBanner;
        coupon.BannerHeadline = r.BannerHeadline;
    }

    private static PlanRow Describe(Plan p, int activeSubscriptions) => new()
    {
        Id = p.Id, Name = p.Name, Slug = p.Slug, Tagline = p.Tagline, Description = p.Description,
        Kind = p.Kind, Cycle = p.Cycle, AccessScope = p.AccessScope, DurationDays = p.DurationDays,
        BasePrice = p.BasePrice, AdmissionFee = p.AdmissionFee, GstRatePercent = p.GstRatePercent,
        SacCode = p.SacCode, ClassCredits = p.ClassCredits, PtSessionCredits = p.PtSessionCredits,
        GuestPasses = p.GuestPasses, FreezeDaysAllowed = p.FreezeDaysAllowed, FreezeFee = p.FreezeFee,
        AccessWindowStart = p.AccessWindowStart?.ToString("HH\\:mm"),
        AccessWindowEnd = p.AccessWindowEnd?.ToString("HH\\:mm"),
        Features = ParseFeatures(p.FeaturesJson),
        TrustMicrocopy = p.TrustMicrocopy, IsMostPopular = p.IsMostPopular,
        ShowOnWebsite = p.ShowOnWebsite, IsActive = p.IsActive, DisplayOrder = p.DisplayOrder,
        ActiveSubscriptions = activeSubscriptions,
        BranchPrices = p.BranchPrices.Select(bp => new PlanBranchPriceRow
        {
            BranchId = bp.BranchId,
            BranchName = bp.Branch?.Name ?? $"Branch {bp.BranchId}",
            Price = bp.Price,
            AdmissionFee = bp.AdmissionFee,
            IsAvailable = bp.IsAvailable
        }).OrderBy(bp => bp.BranchName).ToList()
    };

    private static CouponRow Describe(Coupon c, DateOnly today) => new()
    {
        Id = c.Id, Code = c.Code, Name = c.Name, Description = c.Description,
        DiscountType = c.DiscountType, DiscountValue = c.DiscountValue,
        MaxDiscountAmount = c.MaxDiscountAmount, MinOrderAmount = c.MinOrderAmount,
        ValidFrom = c.ValidFrom.ToString("yyyy-MM-dd"), ValidTo = c.ValidTo.ToString("yyyy-MM-dd"),
        UsageCap = c.UsageCap, UsageCount = c.UsageCount, PerMemberCap = c.PerMemberCap,
        BranchScope = c.BranchScope, PlanScope = c.PlanScope, IsActive = c.IsActive,
        ShowAsWebsiteBanner = c.ShowAsWebsiteBanner, BannerHeadline = c.BannerHeadline,
        IsLive = c.IsActive && today >= c.ValidFrom && today <= c.ValidTo
                 && (c.UsageCap == null || c.UsageCount < c.UsageCap)
    };

    private static IReadOnlyList<string> ParseFeatures(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>(); }
        catch (JsonException) { return Array.Empty<string>(); }
    }
}

public record CancelSubscriptionRequest
{
    public string Reason { get; init; } = "Cancelled by the desk";
}
