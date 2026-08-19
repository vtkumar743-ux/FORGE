using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;

namespace Gym.Infrastructure.Services;

/// <summary>
/// Builds the shareable body-scan PDF (Module 4.4). Every figure is read here, before the
/// document is composed, because QuestPDF renders synchronously — a lazy-loaded navigation
/// property inside the layout would either deadlock or silently print nothing.
/// </summary>
public class ProgressReportService
{
    private readonly GymDbContext _db;
    private readonly IClock _clock;

    public ProgressReportService(GymDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<(byte[] Pdf, string FileName)?> BuildBodyScanReportAsync(
        int memberId, string? coachNote = null, CancellationToken ct = default)
    {
        var member = await _db.Members.AsNoTracking()
            .Include(m => m.HomeBranch)
            .FirstOrDefaultAsync(m => m.Id == memberId, ct);
        if (member is null) return null;

        // Eight scans is what the spec asks the chart to show, and it is also about as many
        // rows as fit on one page without the table turning into a spreadsheet.
        var scans = await _db.BodyScans.AsNoTracking()
            .Where(s => s.MemberId == memberId)
            .OrderByDescending(s => s.ScanDate)
            .Take(8)
            .ToListAsync(ct);
        scans.Reverse();

        var since = scans.Count > 0
            ? scans[0].ScanDate.ToDateTime(TimeOnly.MinValue).AddHours(-5.5)
            : _clock.UtcNow.AddDays(-90);

        var checkIns = await _db.CheckIns.AsNoTracking()
            .CountAsync(c => c.MemberId == memberId && !c.WasBlocked && c.CheckInAtUtc >= since, ct);

        var workouts = await _db.WorkoutLogs.AsNoTracking()
            .Where(l => l.MemberId == memberId && l.PerformedAtUtc >= since)
            .Select(l => l.PerformedOn)
            .Distinct()
            .CountAsync(ct);

        var prs = await _db.WorkoutLogs.AsNoTracking()
            .CountAsync(l => l.MemberId == memberId && l.IsPersonalRecord && l.PerformedAtUtc >= since, ct);

        var strength = await _db.WorkoutLogs.AsNoTracking()
            .Where(l => l.MemberId == memberId && l.Exercise.IsStrengthTracked && l.EstimatedOneRepMax > 0)
            .GroupBy(l => l.Exercise.Name)
            .Select(g => new
            {
                Exercise = g.Key,
                // First and latest by date, not by magnitude: this column is a story about
                // change over time, so an early fluke must not become the "first" figure.
                FirstOn = g.Min(x => x.PerformedOn),
                LatestOn = g.Max(x => x.PerformedOn),
                Best = g.Max(x => x.EstimatedOneRepMax)
            })
            .OrderByDescending(x => x.Best)
            .Take(6)
            .ToListAsync(ct);

        var rows = new List<StrengthRow>();
        foreach (var lift in strength)
        {
            var firstE1Rm = await _db.WorkoutLogs.AsNoTracking()
                .Where(l => l.MemberId == memberId && l.Exercise.Name == lift.Exercise && l.PerformedOn == lift.FirstOn)
                .MaxAsync(l => (decimal?)l.EstimatedOneRepMax, ct) ?? 0m;
            var latestE1Rm = await _db.WorkoutLogs.AsNoTracking()
                .Where(l => l.MemberId == memberId && l.Exercise.Name == lift.Exercise && l.PerformedOn == lift.LatestOn)
                .MaxAsync(l => (decimal?)l.EstimatedOneRepMax, ct) ?? 0m;

            rows.Add(new StrengthRow(lift.Exercise, Math.Round(firstE1Rm, 1), Math.Round(latestE1Rm, 1), lift.LatestOn));
        }

        var model = new BodyScanReportModel
        {
            MemberName = member.FullName,
            MemberCode = member.MemberCode,
            BranchName = member.HomeBranch.Name,
            GeneratedOn = _clock.LocalNow.ToString("dd MMM yyyy"),
            Scans = scans,
            HeightCm = member.HeightCm,
            Goal = member.PrimaryGoal,
            CheckInsInPeriod = checkIns,
            WorkoutsInPeriod = workouts,
            PersonalRecords = prs,
            CurrentStreakDays = member.CurrentStreakDays,
            Strength = rows,
            CoachNote = coachNote
        };

        var bytes = new BodyScanReport(model).GeneratePdf();
        var fileName = $"FORGE-progress-{member.MemberCode}-{_clock.Today:yyyy-MM-dd}.pdf";
        return (bytes, fileName);
    }
}
