using Gym.Core.Entities;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers.Admin;

/// <summary>
/// The seasonal offer engine (Module 4.7). A campaign is a coupon — the same row the point of
/// sale validates — so what the website advertises and what the desk can actually apply are
/// the same object. This controller adds the campaign's life: schedule it, launch it, pause
/// it, end it, and see what it earned.
/// </summary>
[ApiController]
[Route("api/admin/offers")]
[Authorize(Roles = RoleNames.Admin)]
[Produces("application/json")]
public class AdminOffersController : ControllerBase
{
    private readonly GymDbContext _db;
    private readonly OfferService _offers;
    private readonly IClock _clock;

    public AdminOffersController(GymDbContext db, OfferService offers, IClock clock)
    {
        _db = db;
        _offers = offers;
        _clock = clock;
    }

    [HttpGet("campaigns")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Campaigns(CancellationToken ct)
    {
        var campaigns = await _offers.CampaignsAsync(ct);
        var today = _clock.Today;

        return Ok(new
        {
            today = today.ToString("yyyy-MM-dd"),
            live = campaigns.Count(c => c.Status == OfferStatus.Live),
            scheduled = campaigns.Count(c => c.Status == OfferStatus.Scheduled),
            // Only live campaigns can be on the banner, so this is the number the public site shows.
            onBanner = campaigns.Count(c => c.ShowAsWebsiteBanner && c.Status == OfferStatus.Live),
            redemptionsAllTime = campaigns.Sum(c => c.Redemptions),
            discountGivenAllTime = campaigns.Sum(c => c.DiscountGiven),
            revenueBookedAllTime = campaigns.Sum(c => c.RevenueBooked),
            campaigns
        });
    }

    /// <summary>
    /// Puts a campaign on the public banner. Only one banner runs at a time — two competing
    /// offers on the same hero is how a visitor ends up choosing neither.
    /// </summary>
    [HttpPost("campaigns/{id:int}/banner")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetBanner(int id, SetBannerRequest request, CancellationToken ct)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (coupon is null) return NotFound();

        if (request.Show)
        {
            var status = OfferService.StatusOf(coupon, _clock.Today);
            if (status is OfferStatus.Expired or OfferStatus.SoldOut)
                return Conflict(new ProblemDetails
                {
                    Title = "That campaign cannot go on the banner",
                    Detail = status == OfferStatus.Expired
                        ? "It has passed its end date. Extend the dates first."
                        : "Every use has been claimed. Raise the cap first."
                });

            var others = await _db.Coupons.Where(c => c.ShowAsWebsiteBanner && c.Id != id).ToListAsync(ct);
            foreach (var other in others) other.ShowAsWebsiteBanner = false;

            coupon.IsActive = true;
        }

        coupon.ShowAsWebsiteBanner = request.Show;
        if (!string.IsNullOrWhiteSpace(request.BannerHeadline)) coupon.BannerHeadline = request.BannerHeadline.Trim();
        coupon.UpdatedAtUtc = _clock.UtcNow;
        coupon.UpdatedBy = User.Identity?.Name;

        await _db.SaveChangesAsync(ct);
        return Ok(new { coupon.Id, coupon.Code, coupon.ShowAsWebsiteBanner, coupon.BannerHeadline });
    }

    /// <summary>Pause takes it off sale immediately; resume puts it back inside its own dates.</summary>
    [HttpPost("campaigns/{id:int}/state")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetState(int id, SetCampaignStateRequest request, CancellationToken ct)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (coupon is null) return NotFound();

        coupon.IsActive = request.Active;
        if (!request.Active) coupon.ShowAsWebsiteBanner = false;
        if (request.EndToday) coupon.ValidTo = _clock.Today;
        if (request.ExtendToDate is { } extend && extend >= _clock.Today) coupon.ValidTo = extend;

        coupon.UpdatedAtUtc = _clock.UtcNow;
        coupon.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            coupon.Id,
            coupon.Code,
            coupon.IsActive,
            validTo = coupon.ValidTo.ToString("yyyy-MM-dd"),
            status = OfferService.StatusOf(coupon, _clock.Today).ToString()
        });
    }

    /// <summary>Retires expired and fully-claimed campaigns now instead of at the next sweep.</summary>
    [HttpPost("sweep")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Sweep(CancellationToken ct)
    {
        var retired = await _offers.SweepAsync(ct);
        return Ok(new { retired });
    }

    /// <summary>
    /// The off-peak tier the spec names (10 AM–4 PM). Listed here rather than only in the plan
    /// catalogue because it is half of "off-peak plans and seasonal offers" — the owner running
    /// a quiet-hours campaign wants both levers on one screen.
    /// </summary>
    [HttpGet("off-peak")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> OffPeak(CancellationToken ct)
    {
        var plans = await _db.Plans.AsNoTracking()
            .Where(p => p.AccessWindowStart != null && p.AccessWindowEnd != null)
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new
            {
                p.Id, p.Name, p.Slug, p.BasePrice, p.IsActive, p.ShowOnWebsite,
                WindowStart = p.AccessWindowStart!.Value.ToString("HH\\:mm"),
                WindowEnd = p.AccessWindowEnd!.Value.ToString("HH\\:mm"),
                ActiveSubscribers = p.Subscriptions.Count(s => s.Status == Core.Enums.SubscriptionStatus.Active)
            })
            .ToListAsync(ct);

        // What the quiet hours are actually worth: refusals tell the owner whether the window
        // is being tested, and the typical-hours chart tells them whether it needs to move.
        var since = _clock.UtcNow.AddDays(-30);
        var offPeakRefusals = await _db.CheckIns.AsNoTracking()
            .CountAsync(c => c.WasBlocked && c.CheckInAtUtc >= since
                             && c.BlockReason != null && c.BlockReason.Contains("off-peak"), ct);

        return Ok(new { plans, offPeakRefusalsLast30Days = offPeakRefusals });
    }
}

public record SetBannerRequest
{
    public bool Show { get; init; }
    public string? BannerHeadline { get; init; }
}

public record SetCampaignStateRequest
{
    public bool Active { get; init; } = true;
    public bool EndToday { get; init; }
    public DateOnly? ExtendToDate { get; init; }
}
