using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.Services;

public record PriceQuote(
    decimal ListPrice,
    decimal AdmissionFee,
    decimal DiscountAmount,
    decimal ProrationCredit,
    decimal Payable,
    int? CouponId,
    string? CouponCode,
    string? CouponMessage,
    DateOnly StartsOn,
    DateOnly EndsOn);

public record SaleResult(Subscription Subscription, Invoice Invoice);

/// <summary>
/// The membership lifecycle: quote, sell, renew, freeze, cancel and upgrade. Every path that
/// takes money produces an invoice through <see cref="InvoiceService"/>, so there is exactly
/// one way money enters the system regardless of which admin screen started it.
/// </summary>
public class SubscriptionService
{
    private readonly GymDbContext _db;
    private readonly InvoiceService _invoices;
    private readonly IClock _clock;
    private readonly ILogger<SubscriptionService> _log;

    public SubscriptionService(
        GymDbContext db, InvoiceService invoices, IClock clock, ILogger<SubscriptionService> log)
    {
        _db = db;
        _invoices = invoices;
        _clock = clock;
        _log = log;
    }

    /// <summary>
    /// What the member would actually be charged: branch override beats list price, an
    /// admission fee applies only to a first membership, an unexpired plan is prorated as a
    /// credit on an upgrade, and the coupon is validated against every one of its caps.
    /// </summary>
    public async Task<PriceQuote> QuoteAsync(
        int memberId, int planId, int branchId, DateOnly startsOn,
        string? couponCode = null, int? upgradeFromSubscriptionId = null, CancellationToken ct = default)
    {
        var plan = await _db.Plans.AsNoTracking().FirstAsync(p => p.Id == planId, ct);

        var override_ = await _db.PlanBranchPrices.AsNoTracking()
            .Where(p => p.PlanId == planId && p.BranchId == branchId && p.IsAvailable)
            .Select(p => new { p.Price, p.AdmissionFee })
            .FirstOrDefaultAsync(ct);

        var listPrice = override_?.Price ?? plan.BasePrice;

        // Admission is charged once per member across the network, not once per plan.
        var hasHistory = await _db.Subscriptions.AsNoTracking()
            .AnyAsync(s => s.MemberId == memberId && s.Status != SubscriptionStatus.Pending, ct);
        var admission = hasHistory ? 0m : override_?.AdmissionFee ?? plan.AdmissionFee;

        var proration = 0m;
        if (upgradeFromSubscriptionId is { } fromId)
        {
            var from = await _db.Subscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == fromId, ct);
            if (from is not null && from.EndsOn > startsOn)
            {
                var totalDays = Math.Max(1, from.EndsOn.DayNumber - from.StartsOn.DayNumber);
                var unusedDays = Math.Max(0, from.EndsOn.DayNumber - startsOn.DayNumber);
                proration = GstCalculator.Round(from.PriceCharged * unusedDays / totalDays);
            }
        }

        var (discount, couponId, code, message) =
            await EvaluateCouponAsync(couponCode, memberId, planId, branchId, listPrice, ct);

        var payable = Math.Max(0m, GstCalculator.Round(listPrice + admission - discount - proration));

        return new PriceQuote(
            listPrice, admission, discount, proration, payable, couponId, code, message,
            startsOn, startsOn.AddDays(Math.Max(1, plan.DurationDays)));
    }

    /// <summary>
    /// Sells a plan: subscription plus its GST invoice, in one unit of work. The member is
    /// promoted out of Lead/Expired the moment an active membership exists.
    /// </summary>
    public async Task<SaleResult> SellAsync(
        int memberId, int planId, int branchId, DateOnly startsOn,
        string? couponCode, int? upgradeFromSubscriptionId, bool autoRenew,
        int dueInDays, string? notes, string? actor, CancellationToken ct = default)
    {
        var member = await _db.Members.FirstAsync(m => m.Id == memberId, ct);
        var branch = await _db.Branches.FirstAsync(b => b.Id == branchId, ct);
        var plan = await _db.Plans.FirstAsync(p => p.Id == planId, ct);

        var quote = await QuoteAsync(memberId, planId, branchId, startsOn, couponCode, upgradeFromSubscriptionId, ct);

        var subscription = new Subscription
        {
            MemberId = memberId,
            PlanId = planId,
            BranchId = branchId,
            Status = SubscriptionStatus.Active,
            StartsOn = quote.StartsOn,
            EndsOn = quote.EndsOn,
            PriceCharged = quote.Payable,
            DiscountAmount = quote.DiscountAmount,
            CouponId = quote.CouponId,
            ClassCreditsRemaining = plan.ClassCredits ?? 0,
            PtCreditsRemaining = plan.PtSessionCredits ?? 0,
            AutoRenew = autoRenew,
            NextBillingOn = autoRenew ? quote.EndsOn : null,
            UpgradedFromSubscriptionId = upgradeFromSubscriptionId,
            CreatedBy = actor
        };
        _db.Subscriptions.Add(subscription);

        if (upgradeFromSubscriptionId is { } fromId)
        {
            var previous = await _db.Subscriptions.FirstOrDefaultAsync(s => s.Id == fromId, ct);
            if (previous is not null && previous.Status == SubscriptionStatus.Active)
            {
                previous.Status = SubscriptionStatus.Cancelled;
                previous.CancelledOn = startsOn;
                previous.CancellationReason = "Upgraded to " + plan.Name;
            }
        }

        var lines = new List<DraftLine>
        {
            new($"{plan.Name} — {plan.DurationDays} days ({quote.StartsOn:dd MMM yyyy} to {quote.EndsOn:dd MMM yyyy})",
                quote.ListPrice, 1, quote.DiscountAmount, plan.GstRatePercent, plan.SacCode, plan.Id)
        };
        if (quote.AdmissionFee > 0)
            lines.Add(new DraftLine("One-time admission fee", quote.AdmissionFee, 1, 0m, plan.GstRatePercent, plan.SacCode, plan.Id));
        if (quote.ProrationCredit > 0)
            lines.Add(new DraftLine("Credit for unused days on the previous plan",
                -quote.ProrationCredit, 1, 0m, plan.GstRatePercent, plan.SacCode, plan.Id));

        var invoice = await _invoices.BuildAsync(
            member, branch, lines, startsOn, dueInDays, subscriptionId: null, orderId: null,
            notes: notes, customerGstin: null, ct: ct);
        // The subscription id only exists after the insert, so EF wires it by navigation.
        invoice.Subscription = subscription;
        invoice.CreatedBy = actor;
        _db.Invoices.Add(invoice);

        if (quote.CouponId is { } couponId)
        {
            var coupon = await _db.Coupons.FirstAsync(c => c.Id == couponId, ct);
            coupon.UsageCount += 1;
        }

        if (member.Status is MemberStatus.Lead or MemberStatus.Expired or MemberStatus.Cancelled or MemberStatus.Trial)
            member.Status = MemberStatus.Active;

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Sold {Plan} to {Member} for {Amount} — invoice {Invoice}",
            plan.Name, member.MemberCode, quote.Payable, invoice.InvoiceNumber);

        return new SaleResult(subscription, invoice);
    }

    /// <summary>
    /// Freezes a membership and pushes the end date out by the frozen days, so a member never
    /// loses paid time. The plan's own allowance and fee are the policy limits.
    /// </summary>
    public async Task<Subscription> FreezeAsync(
        int subscriptionId, DateOnly from, DateOnly to, string? actor, CancellationToken ct = default)
    {
        var subscription = await _db.Subscriptions
            .Include(s => s.Plan)
            .FirstAsync(s => s.Id == subscriptionId, ct);

        if (to <= from) throw new InvalidOperationException("The freeze must end after it starts.");

        var days = to.DayNumber - from.DayNumber;
        var allowance = subscription.Plan.FreezeDaysAllowed;
        if (allowance > 0 && subscription.FreezeDaysUsed + days > allowance)
            throw new InvalidOperationException(
                $"This plan allows {allowance} freeze days and {subscription.FreezeDaysUsed} are already used.");

        subscription.FreezeStartsOn = from;
        subscription.FreezeEndsOn = to;
        subscription.FreezeDaysUsed += days;
        // Paid-for days are returned at the far end rather than lost to the freeze.
        subscription.EndsOn = subscription.EndsOn.AddDays(days);
        subscription.UpdatedBy = actor;

        // A freeze booked for next month must not lock the member out this afternoon. The
        // window is recorded now and the operations sweep flips the status on the day it
        // opens — which is also what closes it again when it ends.
        if (from <= _clock.Today)
        {
            subscription.Status = SubscriptionStatus.Frozen;
            var member = await _db.Members.FirstAsync(m => m.Id == subscription.MemberId, ct);
            member.Status = MemberStatus.Frozen;
        }

        await _db.SaveChangesAsync(ct);
        return subscription;
    }

    public async Task<Subscription> ResumeAsync(int subscriptionId, string? actor, CancellationToken ct = default)
    {
        var subscription = await _db.Subscriptions.FirstAsync(s => s.Id == subscriptionId, ct);
        var today = _clock.Today;

        // Ending a freeze early hands back only the days that were actually frozen.
        if (subscription.FreezeEndsOn is { } plannedEnd && plannedEnd > today && subscription.FreezeStartsOn is { } start)
        {
            var unused = plannedEnd.DayNumber - Math.Max(today.DayNumber, start.DayNumber);
            subscription.EndsOn = subscription.EndsOn.AddDays(-unused);
            subscription.FreezeDaysUsed = Math.Max(0, subscription.FreezeDaysUsed - unused);
            subscription.FreezeEndsOn = today;
        }

        subscription.Status = subscription.EndsOn < today ? SubscriptionStatus.Expired : SubscriptionStatus.Active;
        subscription.UpdatedBy = actor;

        var member = await _db.Members.FirstAsync(m => m.Id == subscription.MemberId, ct);
        member.Status = subscription.Status == SubscriptionStatus.Active ? MemberStatus.Active : MemberStatus.Expired;

        await _db.SaveChangesAsync(ct);
        return subscription;
    }

    public async Task<Subscription> CancelAsync(
        int subscriptionId, string reason, string? actor, CancellationToken ct = default)
    {
        var subscription = await _db.Subscriptions.FirstAsync(s => s.Id == subscriptionId, ct);
        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.CancelledOn = _clock.Today;
        subscription.CancellationReason = reason;
        subscription.AutoRenew = false;
        subscription.NextBillingOn = null;
        subscription.UpdatedBy = actor;

        var stillActive = await _db.Subscriptions
            .AnyAsync(s => s.MemberId == subscription.MemberId && s.Id != subscriptionId
                        && s.Status == SubscriptionStatus.Active, ct);
        if (!stillActive)
        {
            var member = await _db.Members.FirstAsync(m => m.Id == subscription.MemberId, ct);
            member.Status = MemberStatus.Cancelled;
        }

        await _db.SaveChangesAsync(ct);
        return subscription;
    }

    /// <summary>
    /// Validates a coupon against date window, usage cap, per-member cap, minimum order and
    /// branch/plan scope. Returns the reason when it does not apply, so the desk can explain.
    /// </summary>
    public async Task<(decimal Discount, int? CouponId, string? Code, string? Message)> EvaluateCouponAsync(
        string? code, int memberId, int planId, int branchId, decimal amount, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return (0m, null, null, null);

        var normalised = code.Trim().ToUpperInvariant();
        var coupon = await _db.Coupons.AsNoTracking().FirstOrDefaultAsync(c => c.Code == normalised, ct);
        if (coupon is null) return (0m, null, normalised, "No coupon with that code.");
        if (!coupon.IsActive) return (0m, null, normalised, "That coupon has been switched off.");

        var today = _clock.Today;
        if (today < coupon.ValidFrom) return (0m, null, normalised, $"Valid from {coupon.ValidFrom:dd MMM yyyy}.");
        if (today > coupon.ValidTo) return (0m, null, normalised, $"Expired on {coupon.ValidTo:dd MMM yyyy}.");
        if (coupon.UsageCap is { } cap && coupon.UsageCount >= cap) return (0m, null, normalised, "Fully redeemed.");
        if (amount < coupon.MinOrderAmount)
            return (0m, null, normalised, $"Applies from {coupon.MinOrderAmount:N0} upwards.");

        if (!InScope(coupon.BranchScope, branchId)) return (0m, null, normalised, "Not valid at this branch.");
        if (!InScope(coupon.PlanScope, planId)) return (0m, null, normalised, "Not valid on this plan.");

        if (coupon.PerMemberCap is { } perMember)
        {
            var used = await _db.Subscriptions.CountAsync(s => s.MemberId == memberId && s.CouponId == coupon.Id, ct);
            if (used >= perMember) return (0m, null, normalised, "Already used by this member.");
        }

        var discount = coupon.DiscountType == DiscountType.Percentage
            ? GstCalculator.Round(amount * coupon.DiscountValue / 100m)
            : coupon.DiscountValue;
        if (coupon.MaxDiscountAmount is { } max) discount = Math.Min(discount, max);
        discount = Math.Min(discount, amount);

        return (discount, coupon.Id, normalised, null);
    }

    /// <summary>Null scope means "everywhere"; otherwise it is a comma-separated id list.</summary>
    private static bool InScope(string? scope, int id) =>
        string.IsNullOrWhiteSpace(scope) ||
        scope.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
             .Any(part => int.TryParse(part, out var value) && value == id);
}
