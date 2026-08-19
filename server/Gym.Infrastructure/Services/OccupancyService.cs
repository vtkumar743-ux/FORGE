using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gym.Infrastructure.Services;

/// <summary>
/// The one place a branch's head-count is computed (Module 4.1). The REST endpoint, the
/// SignalR push and the admin dashboard all call it, so a visitor refreshing the page and a
/// visitor holding a socket open can never be shown two different numbers.
/// </summary>
public class OccupancyService
{
    private readonly GymDbContext _db;
    private readonly IClock _clock;

    public OccupancyService(GymDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<BranchOccupancySnapshot>> AllAsync(CancellationToken ct = default)
    {
        var branches = await _db.Branches.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.DisplayOrder)
            .Select(b => new { b.Id, b.Name, b.Slug, b.OccupancyCapacity })
            .ToListAsync(ct);

        var counts = await _db.CheckIns.AsNoTracking()
            .Where(c => c.CheckOutAtUtc == null && !c.WasBlocked)
            .GroupBy(c => c.BranchId)
            .Select(g => new { BranchId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BranchId, x => x.Count, ct);

        var asOf = _clock.UtcNow;
        return branches
            .Select(b => Build(b.Id, b.Name, b.Slug, counts.GetValueOrDefault(b.Id), b.OccupancyCapacity, asOf))
            .ToList();
    }

    public async Task<BranchOccupancySnapshot?> ForBranchAsync(int branchId, CancellationToken ct = default)
    {
        var branch = await _db.Branches.AsNoTracking()
            .Where(b => b.Id == branchId)
            .Select(b => new { b.Id, b.Name, b.Slug, b.OccupancyCapacity })
            .FirstOrDefaultAsync(ct);
        if (branch is null) return null;

        var current = await _db.CheckIns.AsNoTracking()
            .CountAsync(c => c.BranchId == branchId && c.CheckOutAtUtc == null && !c.WasBlocked, ct);

        return Build(branch.Id, branch.Name, branch.Slug, current, branch.OccupancyCapacity, _clock.UtcNow);
    }

    /// <summary>
    /// "Typically busy at 7 PM" — the hourly average over the last eight weeks, per weekday.
    /// A live gauge alone tells someone whether to come *now*; this tells them when to come,
    /// which is the question people actually have when they open the page at work.
    /// </summary>
    public async Task<IReadOnlyList<TypicalHourRow>> TypicalHoursAsync(int branchId, CancellationToken ct = default)
    {
        var since = _clock.Today.AddDays(-56);
        var sinceUtc = since.ToDateTime(TimeOnly.MinValue).AddHours(-5.5);

        var visits = await _db.CheckIns.AsNoTracking()
            .Where(c => c.BranchId == branchId && !c.WasBlocked && c.CheckInAtUtc >= sinceUtc)
            .Select(c => c.CheckInAtUtc)
            .ToListAsync(ct);

        var capacity = await _db.Branches.AsNoTracking()
            .Where(b => b.Id == branchId).Select(b => b.OccupancyCapacity).FirstOrDefaultAsync(ct);
        if (capacity <= 0) capacity = 120;

        // IST wall clock: the reader is standing in Bengaluru, not in UTC.
        var buckets = visits
            .Select(utc => utc.AddHours(5.5))
            .GroupBy(ist => new { Day = (int)ist.DayOfWeek, Hour = ist.Hour })
            .ToDictionary(g => (g.Key.Day, g.Key.Hour), g => g.Count());

        // Weeks observed per weekday, so a partial final week does not read as a quiet day.
        var weeks = Math.Max(1, (int)Math.Ceiling((_clock.Today.DayNumber - since.DayNumber) / 7d));

        var rows = new List<TypicalHourRow>();
        for (var day = 0; day < 7; day++)
        {
            for (var hour = 5; hour <= 23; hour++)
            {
                var total = buckets.GetValueOrDefault((day, hour));
                var average = (double)total / weeks;
                // A visit occupies the floor for roughly an hour, so arrivals-per-hour is a
                // fair stand-in for concurrent head-count without replaying every session.
                var percent = (int)Math.Round(Math.Min(1d, average / capacity) * 100);
                rows.Add(new TypicalHourRow(day, hour, Math.Round(average, 1), percent));
            }
        }
        return rows;
    }

    private static BranchOccupancySnapshot Build(
        int id, string name, string slug, int current, int capacity, DateTime asOf)
    {
        var ratio = capacity == 0 ? 0d : (double)current / capacity;
        return new BranchOccupancySnapshot
        {
            BranchId = id,
            BranchName = name,
            BranchSlug = slug,
            CurrentCount = current,
            Capacity = capacity,
            PercentFull = (int)Math.Round(Math.Min(1d, ratio) * 100),
            Band = ratio switch { < 0.45 => 0, < 0.75 => 1, _ => 2 },
            AsOfUtc = asOf
        };
    }
}

public record TypicalHourRow(int DayOfWeek, int Hour, double AverageVisits, int PercentOfCapacity);
