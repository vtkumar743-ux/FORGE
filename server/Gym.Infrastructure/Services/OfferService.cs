using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.Services;

/// <summary>
/// The seasonal offer engine (Module 4.7). A campaign is a coupon — the same row the point
/// of sale already validates — so an offer the website advertises and the discount the desk
/// applies can never drift apart. What this adds is the campaign's life: it goes live on its
/// own date, retires on its own date, and carries back what it actually earned.
/// </summary>
public class OfferService
{
    private readonly GymDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<OfferService> _log;

    public OfferService(GymDbContext db, IClock clock, ILogger<OfferService> log)
    {
        _db = db;
        _clock = clock;
        _log = log;
    }

    /// <summary>
    /// Retires expired and fully-redeemed campaigns. Run by the operations sweep, so a banner
    /// for a Diwali offer stops advertising a discount the point of sale would refuse.
    /// </summary>
    public async Task<int> SweepAsync(CancellationToken ct = default)
    {
        var today = _clock.Today;

        var stale = await _db.Coupons
            .Where(c => c.IsActive && (c.ValidTo < today || (c.UsageCap != null && c.UsageCount >= c.UsageCap)))
            .ToListAsync(ct);

        foreach (var coupon in stale)
        {
            coupon.IsActive = false;
            coupon.ShowAsWebsiteBanner = false;
        }

        if (stale.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("Offer engine retired {Count} campaigns", stale.Count);
        }
        return stale.Count;
    }

    /// <summary>
    /// Every campaign with what it did: redemptions, the discount given away and the revenue
    /// booked against it. A campaign nobody measures gets repeated whether or not it worked.
    /// </summary>
    public async Task<IReadOnlyList<OfferCampaign>> CampaignsAsync(CancellationToken ct = default)
    {
        var today = _clock.Today;
        var coupons = await _db.Coupons.AsNoTracking().OrderByDescending(c => c.ValidFrom).ThenBy(c => c.Code).ToListAsync(ct);
        if (coupons.Count == 0) return Array.Empty<OfferCampaign>();

        var ids = coupons.Select(c => c.Id).ToList();
        var performance = await _db.Subscriptions.AsNoTracking()
            .Where(s => s.CouponId != null && ids.Contains(s.CouponId!.Value))
            .GroupBy(s => s.CouponId!.Value)
            .Select(g => new
            {
                CouponId = g.Key,
                Redemptions = g.Count(),
                Discount = g.Sum(x => x.DiscountAmount),
                Revenue = g.Sum(x => x.PriceCharged)
            })
            .ToDictionaryAsync(x => x.CouponId, x => x, ct);

        var branchNames = await _db.Branches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        var planNames = await _db.Plans.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        return coupons.Select(c =>
        {
            var stats = performance.GetValueOrDefault(c.Id);
            return new OfferCampaign
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                Description = c.Description,
                BannerHeadline = c.BannerHeadline,
                DiscountType = c.DiscountType,
                DiscountValue = c.DiscountValue,
                MaxDiscountAmount = c.MaxDiscountAmount,
                MinOrderAmount = c.MinOrderAmount,
                ValidFrom = c.ValidFrom,
                ValidTo = c.ValidTo,
                UsageCap = c.UsageCap,
                UsageCount = c.UsageCount,
                PerMemberCap = c.PerMemberCap,
                IsActive = c.IsActive,
                ShowAsWebsiteBanner = c.ShowAsWebsiteBanner,
                Status = StatusOf(c, today),
                BranchNames = Names(c.BranchScope, branchNames),
                PlanNames = Names(c.PlanScope, planNames),
                Redemptions = stats?.Redemptions ?? 0,
                DiscountGiven = stats?.Discount ?? 0m,
                RevenueBooked = stats?.Revenue ?? 0m,
                DaysRemaining = c.ValidTo.DayNumber - today.DayNumber
            };
        }).ToList();
    }

    public static OfferStatus StatusOf(Coupon c, DateOnly today)
    {
        if (!c.IsActive) return OfferStatus.Paused;
        if (c.UsageCap is { } cap && c.UsageCount >= cap) return OfferStatus.SoldOut;
        if (c.ValidFrom > today) return OfferStatus.Scheduled;
        if (c.ValidTo < today) return OfferStatus.Expired;
        return OfferStatus.Live;
    }

    private static IReadOnlyList<string> Names(string? scope, IReadOnlyDictionary<int, string> lookup)
    {
        if (string.IsNullOrWhiteSpace(scope)) return Array.Empty<string>();
        return scope.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var id) && lookup.TryGetValue(id, out var name) ? name : part)
            .ToList();
    }
}

public enum OfferStatus { Scheduled = 0, Live = 1, Expired = 2, SoldOut = 3, Paused = 4 }

public record OfferCampaign
{
    public required int Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? BannerHeadline { get; init; }
    public required DiscountType DiscountType { get; init; }
    public required decimal DiscountValue { get; init; }
    public decimal? MaxDiscountAmount { get; init; }
    public decimal MinOrderAmount { get; init; }
    public required DateOnly ValidFrom { get; init; }
    public required DateOnly ValidTo { get; init; }
    public int? UsageCap { get; init; }
    public int UsageCount { get; init; }
    public int? PerMemberCap { get; init; }
    public bool IsActive { get; init; }
    public bool ShowAsWebsiteBanner { get; init; }
    public required OfferStatus Status { get; init; }
    public IReadOnlyList<string> BranchNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PlanNames { get; init; } = Array.Empty<string>();
    public int Redemptions { get; init; }
    public decimal DiscountGiven { get; init; }
    public decimal RevenueBooked { get; init; }
    public int DaysRemaining { get; init; }
}
