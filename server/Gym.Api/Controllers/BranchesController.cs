using Gym.Api.Contracts;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Services;
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
    private readonly OccupancyService _occupancy;

    public BranchesController(GymDbContext db, OccupancyService occupancy)
    {
        _db = db;
        _occupancy = occupancy;
    }

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
    /// Current head-count per branch, derived from check-ins with no check-out. This is the
    /// snapshot a first paint reads; the same payload is then pushed over the occupancy hub
    /// as people scan in and out, so polling and the socket can never disagree.
    /// </summary>
    [HttpGet("occupancy")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<BranchOccupancyResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BranchOccupancyResponse>>> Occupancy(CancellationToken ct)
    {
        var snapshots = await _occupancy.AllAsync(ct);
        return Ok(snapshots.Select(Map).ToList());
    }

    /// <summary>
    /// Typical busy hours for one branch — the eight-week hourly average, on the IST wall
    /// clock. The live gauge answers "should I come now"; this answers "when should I come",
    /// which is the question someone actually has when they open the page from their desk.
    /// </summary>
    [HttpGet("{slug}/typical-hours")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TypicalHoursResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TypicalHoursResponse>> TypicalHours(string slug, CancellationToken ct)
    {
        var branch = await _db.Branches.AsNoTracking()
            .Where(b => b.Slug == slug && b.IsActive)
            .Select(b => new { b.Id, b.Name, b.Slug })
            .FirstOrDefaultAsync(ct);
        if (branch is null) return NotFound();

        var rows = await _occupancy.TypicalHoursAsync(branch.Id, ct);
        var live = await _occupancy.ForBranchAsync(branch.Id, ct);

        var busiest = rows.Count == 0 ? null : rows.OrderByDescending(r => r.PercentOfCapacity).First();
        var quietest = rows.Where(r => r.Hour is >= 6 and <= 21).OrderBy(r => r.PercentOfCapacity).FirstOrDefault();

        return Ok(new TypicalHoursResponse
        {
            BranchId = branch.Id,
            BranchName = branch.Name,
            BranchSlug = branch.Slug,
            Live = live is null ? null : Map(live),
            Hours = rows.Select(r => new TypicalHourPoint
            {
                DayOfWeek = r.DayOfWeek,
                Hour = r.Hour,
                Label = FormatHour(r.Hour),
                AverageVisits = r.AverageVisits,
                PercentOfCapacity = r.PercentOfCapacity
            }).ToList(),
            BusiestLabel = busiest is null ? null : $"{DayName(busiest.DayOfWeek)} around {FormatHour(busiest.Hour)}",
            QuietestLabel = quietest is null ? null : $"{DayName(quietest.DayOfWeek)} around {FormatHour(quietest.Hour)}"
        });
    }

    private static BranchOccupancyResponse Map(BranchOccupancySnapshot s) => new()
    {
        BranchId = s.BranchId,
        BranchName = s.BranchName,
        BranchSlug = s.BranchSlug,
        CurrentCount = s.CurrentCount,
        Capacity = s.Capacity,
        PercentFull = s.PercentFull,
        Band = (OccupancyBand)s.Band,
        AsOfUtc = s.AsOfUtc
    };

    private static string FormatHour(int hour) => hour switch
    {
        0 => "12 AM",
        < 12 => $"{hour} AM",
        12 => "12 PM",
        _ => $"{hour - 12} PM"
    };

    private static string DayName(int dayOfWeek) =>
        ((DayOfWeek)dayOfWeek).ToString();
}

public record TypicalHoursResponse
{
    public required int BranchId { get; init; }
    public required string BranchName { get; init; }
    public required string BranchSlug { get; init; }
    public BranchOccupancyResponse? Live { get; init; }
    public required IReadOnlyList<TypicalHourPoint> Hours { get; init; }
    public string? BusiestLabel { get; init; }
    public string? QuietestLabel { get; init; }
}

public record TypicalHourPoint
{
    public required int DayOfWeek { get; init; }
    public required int Hour { get; init; }
    public required string Label { get; init; }
    public required double AverageVisits { get; init; }
    public required int PercentOfCapacity { get; init; }
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
