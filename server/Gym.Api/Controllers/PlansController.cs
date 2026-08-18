using System.Text.Json;
using Gym.Api.Contracts;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers;

/// <summary>
/// Public pricing (Module 1.5). Prices resolve per branch — Indiranagar carries a metro
/// premium and Whitefield a discount — so the pricing page always quotes what the visitor
/// would actually be charged at the branch they picked, never a network average.
/// </summary>
[ApiController]
[Route("api/plans")]
[Produces("application/json")]
[AllowAnonymous]
public class PlansController : ControllerBase
{
    private readonly GymDbContext _db;
    private readonly IClock _clock;

    public PlansController(GymDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PlanResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PlanResponse>>> All(
        [FromQuery] string? branchSlug, CancellationToken ct)
    {
        int? branchId = null;
        if (!string.IsNullOrWhiteSpace(branchSlug))
            branchId = await _db.Branches.AsNoTracking()
                .Where(b => b.Slug == branchSlug)
                .Select(b => (int?)b.Id)
                .FirstOrDefaultAsync(ct);

        var plans = await _db.Plans
            .AsNoTracking()
            .Where(p => p.IsActive && p.ShowOnWebsite)
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new
            {
                p.Id, p.Name, p.Slug, p.Tagline, p.Description, p.Kind, p.Cycle, p.AccessScope,
                p.DurationDays, p.BasePrice, p.AdmissionFee, p.GstRatePercent, p.SacCode,
                p.ClassCredits, p.PtSessionCredits, p.GuestPasses, p.FreezeDaysAllowed,
                p.AccessWindowStart, p.AccessWindowEnd, p.FeaturesJson, p.TrustMicrocopy,
                p.IsMostPopular, p.DisplayOrder,
                BranchPrice = branchId == null
                    ? null
                    : p.BranchPrices
                        .Where(bp => bp.BranchId == branchId)
                        .Select(bp => new { bp.Price, bp.AdmissionFee, bp.IsAvailable })
                        .FirstOrDefault()
            })
            .ToListAsync(ct);

        // The monthly plan is the yardstick every longer plan's saving is measured against.
        var monthlyReference = plans
            .Where(p => p.Cycle == BillingCycle.Monthly && p.AccessWindowStart == null)
            .Select(p => p.BranchPrice?.Price ?? p.BasePrice)
            .DefaultIfEmpty(0m)
            .Max();

        var result = plans.Select(p =>
        {
            var price = p.BranchPrice?.Price ?? p.BasePrice;
            var months = Math.Max(1m, Math.Round(p.DurationDays / 30m, 2));
            var perMonth = p.Kind == PlanKind.PtPack ? price : decimal.Round(price / months, 0);

            // Only recurring memberships are comparable with the monthly rate; a PT pack is not.
            var savings = monthlyReference > 0 && p.Kind != PlanKind.PtPack && months > 1 && p.AccessWindowStart == null
                ? (int)Math.Round((1 - perMonth / monthlyReference) * 100)
                : 0;

            return new PlanResponse
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Tagline = p.Tagline,
                Description = p.Description,
                Kind = p.Kind,
                Cycle = p.Cycle,
                CycleName = CycleLabel(p.Cycle),
                AccessScope = p.AccessScope,
                DurationDays = p.DurationDays,
                Price = price,
                BasePrice = p.BasePrice,
                AdmissionFee = p.BranchPrice?.AdmissionFee ?? p.AdmissionFee,
                EffectiveMonthlyPrice = perMonth,
                SavingsPercent = Math.Max(0, savings),
                GstRatePercent = p.GstRatePercent,
                SacCode = p.SacCode,
                ClassCredits = p.ClassCredits,
                PtSessionCredits = p.PtSessionCredits,
                GuestPasses = p.GuestPasses,
                FreezeDaysAllowed = p.FreezeDaysAllowed,
                AccessWindow = p.AccessWindowStart is { } start && p.AccessWindowEnd is { } end
                    ? $"{start:HH\\:mm} – {end:HH\\:mm}"
                    : null,
                Features = ParseFeatures(p.FeaturesJson),
                TrustMicrocopy = p.TrustMicrocopy,
                IsMostPopular = p.IsMostPopular,
                IsAvailableAtBranch = p.BranchPrice?.IsAvailable ?? true,
                DisplayOrder = p.DisplayOrder
            };
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// The seasonal offer behind the pricing banner (Module 1.5). Driven by the coupon
    /// flagged <c>ShowAsWebsiteBanner</c>, so the owner turns the banner on and off by
    /// managing the coupon rather than editing copy in two places.
    /// </summary>
    [HttpGet("offer")]
    [ProducesResponseType(typeof(OfferResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<OfferResponse>> Offer([FromQuery] string? branchSlug, CancellationToken ct)
    {
        var today = _clock.Today;

        var coupons = await _db.Coupons
            .AsNoTracking()
            .Where(c => c.IsActive && c.ShowAsWebsiteBanner && c.ValidFrom <= today && c.ValidTo >= today)
            .Where(c => c.UsageCap == null || c.UsageCount < c.UsageCap)
            .OrderByDescending(c => c.DiscountValue)
            .Select(c => new
            {
                c.Code, c.Name, c.Description, c.DiscountType, c.DiscountValue,
                c.MaxDiscountAmount, c.ValidTo, c.BannerHeadline, c.BranchScope
            })
            .ToListAsync(ct);

        int? branchId = string.IsNullOrWhiteSpace(branchSlug)
            ? null
            : await _db.Branches.AsNoTracking()
                .Where(b => b.Slug == branchSlug).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);

        var coupon = coupons.FirstOrDefault(c =>
            branchId is null ||
            string.IsNullOrWhiteSpace(c.BranchScope) ||
            ClassesController.Split(c.BranchScope).Contains(branchId.Value.ToString()));

        if (coupon is null) return NoContent();

        return Ok(new OfferResponse
        {
            Code = coupon.Code,
            Name = coupon.Name,
            Description = coupon.Description,
            DiscountType = coupon.DiscountType,
            DiscountValue = coupon.DiscountValue,
            MaxDiscountAmount = coupon.MaxDiscountAmount,
            ValidTo = coupon.ValidTo.ToString("yyyy-MM-dd"),
            // The countdown needs an instant: offers lapse at end of day IST.
            ValidToUtc = coupon.ValidTo.AddDays(1).ToDateTime(TimeOnly.MinValue).AddMinutes(-330),
            BannerHeadline = coupon.BannerHeadline
        });
    }

    private static string CycleLabel(BillingCycle cycle) => cycle switch
    {
        BillingCycle.Monthly => "Monthly",
        BillingCycle.Quarterly => "Quarterly",
        BillingCycle.HalfYearly => "Half-yearly",
        BillingCycle.Annual => "Annual",
        _ => "One-off"
    };

    /// <summary>Features are authored as a JSON array; a malformed edit must not 500 the page.</summary>
    private static IReadOnlyList<string> ParseFeatures(string json)
    {
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
