using Gym.Api.Contracts;
using Gym.Core.Enums;
using Gym.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers;

/// <summary>
/// Public branch directory plus the derived occupancy reading. The live push over SignalR
/// (Module 4.1) uses the same shape, so the client renders one component either way.
/// </summary>
[ApiController]
[Route("api/branches")]
[Produces("application/json")]
public class BranchesController : ControllerBase
{
    private readonly GymDbContext _db;

    public BranchesController(GymDbContext db) => _db = db;

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<BranchSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BranchSummaryResponse>>> All(CancellationToken ct)
    {
        var branches = await _db.Branches
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.DisplayOrder)
            .Select(b => new BranchSummaryResponse
            {
                Id = b.Id, Name = b.Name, Slug = b.Slug, City = b.City,
                AddressLine1 = b.AddressLine1, AddressLine2 = b.AddressLine2, Pincode = b.Pincode,
                Phone = b.Phone, WhatsAppNumber = b.WhatsAppNumber, Email = b.Email,
                Latitude = b.Latitude, Longitude = b.Longitude, GoogleMapsUrl = b.GoogleMapsUrl,
                HeroImageUrl = b.HeroImageUrl, ShortPitch = b.ShortPitch,
                WeekdayHours = $"{b.WeekdayOpen:HH\\:mm} – {b.WeekdayClose:HH\\:mm}",
                WeekendHours = $"{b.WeekendOpen:HH\\:mm} – {b.WeekendClose:HH\\:mm}",
                OccupancyCapacity = b.OccupancyCapacity, DisplayOrder = b.DisplayOrder
            })
            .ToListAsync(ct);

        return Ok(branches);
    }

    [HttpGet("{slug}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(BranchSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BranchSummaryResponse>> BySlug(string slug, CancellationToken ct)
    {
        var branch = await _db.Branches
            .AsNoTracking()
            .Where(b => b.Slug == slug && b.IsActive)
            .Select(b => new BranchSummaryResponse
            {
                Id = b.Id, Name = b.Name, Slug = b.Slug, City = b.City,
                AddressLine1 = b.AddressLine1, AddressLine2 = b.AddressLine2, Pincode = b.Pincode,
                Phone = b.Phone, WhatsAppNumber = b.WhatsAppNumber, Email = b.Email,
                Latitude = b.Latitude, Longitude = b.Longitude, GoogleMapsUrl = b.GoogleMapsUrl,
                HeroImageUrl = b.HeroImageUrl, ShortPitch = b.ShortPitch,
                WeekdayHours = $"{b.WeekdayOpen:HH\\:mm} – {b.WeekdayClose:HH\\:mm}",
                WeekendHours = $"{b.WeekendOpen:HH\\:mm} – {b.WeekendClose:HH\\:mm}",
                OccupancyCapacity = b.OccupancyCapacity, DisplayOrder = b.DisplayOrder
            })
            .FirstOrDefaultAsync(ct);

        return branch is null ? NotFound() : Ok(branch);
    }

    /// <summary>
    /// Current head-count per branch, derived from check-ins with no check-out. Phase 1 renders
    /// the SVG gauge from this; Phase 4 pushes the same payload over the occupancy hub.
    /// </summary>
    [HttpGet("occupancy")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<BranchOccupancyResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BranchOccupancyResponse>>> Occupancy(CancellationToken ct)
    {
        var branches = await _db.Branches
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.DisplayOrder)
            .Select(b => new { b.Id, b.Name, b.Slug, b.OccupancyCapacity })
            .ToListAsync(ct);

        var counts = await _db.CheckIns
            .AsNoTracking()
            .Where(c => c.CheckOutAtUtc == null && !c.WasBlocked)
            .GroupBy(c => c.BranchId)
            .Select(g => new { BranchId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BranchId, x => x.Count, ct);

        var result = branches.Select(b =>
        {
            var current = counts.GetValueOrDefault(b.Id);
            var ratio = b.OccupancyCapacity == 0 ? 0d : (double)current / b.OccupancyCapacity;
            return new BranchOccupancyResponse
            {
                BranchId = b.Id,
                BranchName = b.Name,
                BranchSlug = b.Slug,
                CurrentCount = current,
                Capacity = b.OccupancyCapacity,
                PercentFull = (int)Math.Round(Math.Min(1d, ratio) * 100),
                Band = ratio switch { < 0.45 => OccupancyBand.Comfortable, < 0.75 => OccupancyBand.Busy, _ => OccupancyBand.Peak },
                AsOfUtc = DateTime.UtcNow
            };
        }).ToList();

        return Ok(result);
    }
}

public record BranchOccupancyResponse
{
    public required int BranchId { get; init; }
    public required string BranchName { get; init; }
    public required string BranchSlug { get; init; }
    public required int CurrentCount { get; init; }
    public required int Capacity { get; init; }
    public required int PercentFull { get; init; }
    public required OccupancyBand Band { get; init; }
    public required DateTime AsOfUtc { get; init; }
}
