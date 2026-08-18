using Gym.Core.Entities;
using Gym.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Gym.Infrastructure.Persistence.Seeding;

/// <summary>
/// Builds exactly 40 recurring weekly slots across the three branches and materialises
/// concrete sessions for a rolling window. Trainers are drawn only from their own branch's
/// pool and checked against (trainer, day, time) and (room, day, time), so the seeded
/// timetable is genuinely conflict-free rather than merely plausible.
/// </summary>
internal static class TimetableSeed
{
    /// <summary>Weekly slot counts per branch — 15 + 11 + 14 = 40.</summary>
    private static readonly Dictionary<string, int> SlotsPerBranch = new()
    {
        ["koramangala"] = 15, ["indiranagar"] = 11, ["whitefield"] = 14
    };

    /// <summary>Real gym demand: two morning peaks, a quiet off-peak slot, two evening peaks.</summary>
    private static readonly TimeOnly[] SlotTimes =
    {
        new(6, 0), new(7, 15), new(10, 30), new(18, 30), new(19, 45), new(20, 45)
    };

    private static readonly DayOfWeek[] WeekOrder =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    };

    /// <summary>Which rooms a format can actually run in.</summary>
    private static readonly Dictionary<string, string[]> RoomForFormat = new()
    {
        ["strength-foundations"] = new[] { "Strength Floor" },
        ["olympic-lifting-lab"] = new[] { "Strength Floor" },
        ["powerlifting-club"] = new[] { "Strength Floor" },
        ["hiit-45"] = new[] { "Studio One", "Studio Two", "Turf Lane" },
        ["crosstrain-circuit"] = new[] { "Studio One", "Turf Lane", "Strength Floor" },
        ["core-conditioning"] = new[] { "Studio One", "Studio Two" },
        ["spin-room"] = new[] { "Spin Room" },
        ["boxing-fundamentals"] = new[] { "Combat Zone", "Studio One" },
        ["mobility-recovery"] = new[] { "Recovery Suite", "Studio Two", "Studio One" },
        ["yoga-flow"] = new[] { "Studio One", "Studio Two" }
    };

    /// <summary>Formats a trainer is qualified to lead, matched to the seeded specialties.</summary>
    private static readonly Dictionary<string, string[]> FormatsForTrainer = new()
    {
        ["karthik-reddy"] = new[] { "strength-foundations", "powerlifting-club", "crosstrain-circuit" },
        ["sneha-iyer"] = new[] { "hiit-45", "core-conditioning", "crosstrain-circuit" },
        ["imran-sheikh"] = new[] { "boxing-fundamentals", "core-conditioning", "hiit-45" },
        ["divya-nair"] = new[] { "mobility-recovery", "yoga-flow", "core-conditioning" },
        ["rohan-kulkarni"] = new[] { "spin-room", "hiit-45", "crosstrain-circuit" },
        ["aparna-menon"] = new[] { "yoga-flow", "mobility-recovery" },
        ["bharath-gowda"] = new[] { "olympic-lifting-lab", "strength-foundations", "powerlifting-club" },
        ["priya-deshpande"] = new[] { "hiit-45", "core-conditioning", "crosstrain-circuit" }
    };

    public static async Task<List<ClassSchedule>> SchedulesAsync(
        GymDbContext db, List<Branch> branches, List<Room> rooms, List<ClassFormat> formats,
        List<Trainer> trainers, DateOnly today, CancellationToken ct)
    {
        if (await db.ClassSchedules.AnyAsync(ct))
            return await db.ClassSchedules.ToListAsync(ct);

        var formatBySlug = formats.ToDictionary(f => f.Slug);
        var schedules = new List<ClassSchedule>();

        // Occupied (day, time) sets guarantee no trainer or room is ever double-booked.
        var trainerBusy = new HashSet<(int TrainerId, DayOfWeek Day, TimeOnly Time)>();
        var roomBusy = new HashSet<(int RoomId, DayOfWeek Day, TimeOnly Time)>();

        foreach (var branch in branches.OrderBy(b => b.DisplayOrder))
        {
            var target = SlotsPerBranch[branch.Slug];
            var branchRooms = rooms.Where(r => r.BranchId == branch.Id).ToList();
            var branchTrainers = trainers.Where(t => t.PrimaryBranchId == branch.Id).OrderBy(t => t.DisplayOrder).ToList();
            var placed = 0;
            var formatCursor = 0;

            // Walk day × slot in a stable order so the same 40 rows appear on every fresh seed.
            foreach (var day in WeekOrder)
            {
                foreach (var time in SlotTimes)
                {
                    if (placed >= target) break;

                    // Sunday runs a short recovery-only timetable; nothing at 20:45 anywhere.
                    if (day == DayOfWeek.Sunday && time > new TimeOnly(10, 30)) continue;
                    if (time == new TimeOnly(20, 45) && day is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;

                    var candidates = branchTrainers
                        .SelectMany(t => FormatsForTrainer[t.Slug].Select(f => (Trainer: t, Format: formatBySlug[f])))
                        .ToList();

                    // Rotate the starting point so branches do not all lead with the same format.
                    var ordered = candidates
                        .Skip(formatCursor % candidates.Count)
                        .Concat(candidates.Take(formatCursor % candidates.Count))
                        .ToList();

                    foreach (var (trainer, format) in ordered)
                    {
                        if (trainerBusy.Contains((trainer.Id, day, time))) continue;

                        var room = RoomForFormat[format.Slug]
                            .Select(name => branchRooms.FirstOrDefault(r => r.Name == name))
                            .FirstOrDefault(r => r is not null && !roomBusy.Contains((r.Id, day, time)));
                        if (room is null) continue;

                        // Off-peak and Sunday slots run smaller; evening peak fills the room.
                        var capacity = Math.Min(format.DefaultCapacity, room.Capacity);
                        if (time == new TimeOnly(10, 30) || day == DayOfWeek.Sunday)
                            capacity = (int)Math.Ceiling(capacity * 0.7);

                        schedules.Add(new ClassSchedule
                        {
                            BranchId = branch.Id, ClassFormatId = format.Id, TrainerId = trainer.Id, RoomId = room.Id,
                            DayOfWeek = day, StartTime = time, DurationMinutes = format.DefaultDurationMinutes,
                            Capacity = capacity,
                            EffectiveFrom = today.AddDays(-90),
                            BookingOpensHoursBefore = 72,
                            CancelCutoffHoursBefore = format.Slug is "olympic-lifting-lab" or "powerlifting-club" ? 12 : 4,
                            WaitlistEnabled = true,
                            WaitlistCapacity = Math.Max(5, capacity / 2)
                        });

                        trainerBusy.Add((trainer.Id, day, time));
                        roomBusy.Add((room.Id, day, time));
                        placed++;
                        formatCursor += 3;
                        break;
                    }
                }
                if (placed >= target) break;
            }
        }

        db.ClassSchedules.AddRange(schedules);
        await db.SaveChangesAsync(ct);
        return schedules;
    }

    /// <summary>
    /// Materialises one <see cref="ClassSession"/> per schedule per date across the window,
    /// so the timetable, rosters and attendance history all hang off real dates.
    /// </summary>
    public static async Task<List<ClassSession>> SessionsAsync(
        GymDbContext db, List<ClassSchedule> schedules, DateOnly today,
        int daysBack, int daysForward, CancellationToken ct)
    {
        if (await db.ClassSessions.AnyAsync(ct))
            return await db.ClassSessions.ToListAsync(ct);

        var sessions = new List<ClassSession>();
        var from = today.AddDays(-daysBack);
        var to = today.AddDays(daysForward);

        foreach (var schedule in schedules)
        {
            for (var date = from; date <= to; date = date.AddDays(1))
            {
                if (date.DayOfWeek != schedule.DayOfWeek) continue;
                if (date < schedule.EffectiveFrom) continue;

                var startsLocal = date.ToDateTime(schedule.StartTime);
                sessions.Add(new ClassSession
                {
                    ClassScheduleId = schedule.Id,
                    BranchId = schedule.BranchId,
                    ClassFormatId = schedule.ClassFormatId,
                    TrainerId = schedule.TrainerId,
                    RoomId = schedule.RoomId,
                    SessionDate = date,
                    StartTime = schedule.StartTime,
                    DurationMinutes = schedule.DurationMinutes,
                    // IST has no DST, so a fixed offset is exact rather than approximate.
                    StartsAtUtc = startsLocal.AddMinutes(-330),
                    EndsAtUtc = startsLocal.AddMinutes(schedule.DurationMinutes - 330),
                    Capacity = schedule.Capacity,
                    Status = date < today ? SessionStatus.Completed : SessionStatus.Scheduled
                });
            }
        }

        db.ClassSessions.AddRange(sessions);
        await db.SaveChangesAsync(ct);
        return sessions;
    }
}
