using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.Services;

public record ScheduleConflict(string Kind, string Message, int ConflictingScheduleId, string ConflictingLabel);

/// <summary>
/// The recurring timetable: conflict detection before a slot is saved, and materialisation of
/// the concrete <see cref="ClassSession"/> rows that bookings, rosters and no-shows attach to.
/// </summary>
public class SchedulingService
{
    /// <summary>Everything in the product runs on IST; sessions store both the wall clock and the instant.</summary>
    public static readonly TimeSpan IstOffset = TimeSpan.FromMinutes(330);

    private readonly GymDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<SchedulingService> _log;

    public SchedulingService(GymDbContext db, IClock clock, ILogger<SchedulingService> log)
    {
        _db = db;
        _clock = clock;
        _log = log;
    }

    /// <summary>
    /// A trainer cannot be in two rooms at once and a room cannot hold two classes at once.
    /// Both are checked as interval overlaps on the same weekday, ignoring the slot being
    /// edited and any rule whose effective window has already closed.
    /// </summary>
    public async Task<IReadOnlyList<ScheduleConflict>> FindConflictsAsync(
        int branchId, int trainerId, int? roomId, DayOfWeek day, TimeOnly startTime, int durationMinutes,
        DateOnly effectiveFrom, DateOnly? effectiveTo, int? ignoreScheduleId, CancellationToken ct = default)
    {
        var endTime = startTime.AddMinutes(durationMinutes);

        var candidates = await _db.ClassSchedules
            .AsNoTracking()
            .Where(s => s.IsActive && s.DayOfWeek == day)
            .Where(s => ignoreScheduleId == null || s.Id != ignoreScheduleId)
            .Where(s => s.TrainerId == trainerId || (roomId != null && s.RoomId == roomId))
            .Select(s => new
            {
                s.Id, s.TrainerId, s.RoomId, s.BranchId, s.StartTime, s.DurationMinutes,
                s.EffectiveFrom, s.EffectiveTo,
                TrainerName = s.Trainer.FullName,
                RoomName = s.Room != null ? s.Room.Name : null,
                FormatName = s.ClassFormat.Name,
                BranchName = s.Branch.Name
            })
            .ToListAsync(ct);

        var conflicts = new List<ScheduleConflict>();

        foreach (var other in candidates)
        {
            // Two rules only clash if their effective windows overlap as well as their times.
            var windowsOverlap = other.EffectiveFrom <= (effectiveTo ?? DateOnly.MaxValue)
                              && effectiveFrom <= (other.EffectiveTo ?? DateOnly.MaxValue);
            if (!windowsOverlap) continue;

            var otherEnd = other.StartTime.AddMinutes(other.DurationMinutes);
            if (startTime >= otherEnd || endTime <= other.StartTime) continue;

            var label = $"{other.FormatName} · {other.StartTime:HH\\:mm} · {other.BranchName}";

            if (other.TrainerId == trainerId)
                conflicts.Add(new ScheduleConflict("trainer",
                    $"{other.TrainerName} already teaches {label}.", other.Id, label));

            if (roomId is not null && other.RoomId == roomId && other.BranchId == branchId)
                conflicts.Add(new ScheduleConflict("room",
                    $"{other.RoomName} is already booked for {label}.", other.Id, label));
        }

        return conflicts;
    }

    /// <summary>
    /// Creates the occurrences for a rule over a date range. The unique index on
    /// (ClassScheduleId, SessionDate) makes this idempotent — running it twice is a no-op.
    /// </summary>
    public async Task<int> MaterialiseAsync(
        ClassSchedule schedule, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var existing = await _db.ClassSessions
            .Where(s => s.ClassScheduleId == schedule.Id && s.SessionDate >= from && s.SessionDate <= to)
            .Select(s => s.SessionDate)
            .ToListAsync(ct);
        var already = existing.ToHashSet();

        var created = 0;
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (date.DayOfWeek != schedule.DayOfWeek) continue;
            if (date < schedule.EffectiveFrom) continue;
            if (schedule.EffectiveTo is { } end && date > end) continue;
            if (already.Contains(date)) continue;

            var startsIst = date.ToDateTime(schedule.StartTime);
            _db.ClassSessions.Add(new ClassSession
            {
                ClassScheduleId = schedule.Id,
                BranchId = schedule.BranchId,
                ClassFormatId = schedule.ClassFormatId,
                TrainerId = schedule.TrainerId,
                RoomId = schedule.RoomId,
                SessionDate = date,
                StartTime = schedule.StartTime,
                DurationMinutes = schedule.DurationMinutes,
                StartsAtUtc = startsIst - IstOffset,
                EndsAtUtc = startsIst.AddMinutes(schedule.DurationMinutes) - IstOffset,
                Capacity = schedule.Capacity,
                Status = SessionStatus.Scheduled
            });
            created++;
        }

        if (created > 0) await _db.SaveChangesAsync(ct);
        return created;
    }

    /// <summary>
    /// Fills freed spots from the waitlist in queue order and renumbers whoever is left, so
    /// position 1 always means next in line. Returns the bookings that were promoted.
    /// </summary>
    public async Task<IReadOnlyList<Booking>> PromoteWaitlistAsync(int sessionId, CancellationToken ct = default)
    {
        var session = await _db.ClassSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || session.Status != SessionStatus.Scheduled) return Array.Empty<Booking>();

        var waiting = await _db.Bookings
            .Where(b => b.ClassSessionId == sessionId && b.Status == BookingStatus.Waitlisted)
            .OrderBy(b => b.WaitlistPosition ?? int.MaxValue)
            .ThenBy(b => b.BookedAtUtc)
            .ToListAsync(ct);

        var promoted = new List<Booking>();
        foreach (var booking in waiting)
        {
            if (session.BookedCount >= session.Capacity) break;
            booking.Status = BookingStatus.Booked;
            booking.WaitlistPosition = null;
            booking.PromotedFromWaitlistAtUtc = _clock.UtcNow;
            session.BookedCount++;
            session.WaitlistCount = Math.Max(0, session.WaitlistCount - 1);
            promoted.Add(booking);
        }

        var position = 1;
        foreach (var booking in waiting.Where(b => b.Status == BookingStatus.Waitlisted))
            booking.WaitlistPosition = position++;

        if (promoted.Count > 0)
            _log.LogInformation("Promoted {Count} member(s) off the waitlist for session {SessionId}",
                promoted.Count, sessionId);

        return promoted;
    }

    /// <summary>
    /// Closes out sessions whose end time has passed: still-booked members who never checked
    /// in become no-shows, and the session moves to Completed. Idempotent by status.
    /// </summary>
    public async Task<(int Sessions, int NoShows)> CloseFinishedSessionsAsync(CancellationToken ct = default)
    {
        var cutoff = _clock.UtcNow.AddMinutes(-30);

        var sessions = await _db.ClassSessions
            .Where(s => s.Status == SessionStatus.Scheduled && s.EndsAtUtc < cutoff)
            .OrderBy(s => s.EndsAtUtc)
            .Take(500)
            .ToListAsync(ct);

        if (sessions.Count == 0) return (0, 0);

        var ids = sessions.Select(s => s.Id).ToList();
        var bookings = await _db.Bookings
            .Where(b => ids.Contains(b.ClassSessionId) && (b.Status == BookingStatus.Booked || b.Status == BookingStatus.Waitlisted))
            .ToListAsync(ct);

        var noShows = 0;
        foreach (var booking in bookings)
        {
            if (booking.Status == BookingStatus.Waitlisted)
            {
                // Never promoted, so it was never a commitment — cancel it, do not penalise.
                booking.Status = BookingStatus.Cancelled;
                booking.CancelledAtUtc = _clock.UtcNow;
                continue;
            }

            booking.Status = booking.CheckedInAtUtc is null ? BookingStatus.NoShow : BookingStatus.Attended;
            if (booking.Status == BookingStatus.NoShow) noShows++;
        }

        foreach (var session in sessions)
        {
            session.Status = SessionStatus.Completed;
            session.AttendedCount = bookings.Count(b => b.ClassSessionId == session.Id && b.Status == BookingStatus.Attended);
        }

        await _db.SaveChangesAsync(ct);
        return (sessions.Count, noShows);
    }
}
