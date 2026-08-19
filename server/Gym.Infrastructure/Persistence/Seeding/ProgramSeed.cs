using Gym.Core.Entities;
using Gym.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Gym.Infrastructure.Persistence.Seeding;

/// <summary>
/// Training programmes: four coach-written templates, plus a published copy assigned to the
/// members who already have logged training history — so the portal's programme viewer opens
/// on real work rather than on an empty state, and the set logger has a day to log against.
///
/// Templates are the master rows (no member); an assignment is a deep copy, because a member's
/// plan must not change under them when the coach edits the template next month.
/// </summary>
internal static class ProgramSeed
{
    private record DaySpec(string Title, string Focus, bool Rest, (string Exercise, int Sets, string Reps, int RestSeconds, string? Superset)[] Work);
    private record TemplateSpec(string Name, string Goal, string Description, int Weeks, int DaysPerWeek, DaySpec[] Days);

    public static async Task SeedAsync(
        GymDbContext db, List<Member> members, List<Trainer> trainers, DateOnly today,
        Random rng, CancellationToken ct)
    {
        if (await db.WorkoutPrograms.AnyAsync(ct)) return;

        var exercises = await db.Exercises.AsNoTracking().ToDictionaryAsync(e => e.Name, ct);
        if (exercises.Count == 0) return;

        var templates = Templates();
        var created = new List<WorkoutProgram>();

        foreach (var (spec, index) in templates.Select((s, i) => (s, i)))
        {
            var coach = trainers[index % trainers.Count];
            created.Add(Build(spec, exercises, memberId: null, trainerId: coach.Id, isTemplate: true,
                status: ProgramStatus.Published, startsOn: null, today));
        }

        db.WorkoutPrograms.AddRange(created);
        await db.SaveChangesAsync(ct);

        // Assign to the members who actually train: the ones the training-history seeder gave
        // logs to. A programme on a dormant member is a screen nobody will ever open.
        var trainingMemberIds = await db.WorkoutLogs
            .Select(l => l.MemberId)
            .Distinct()
            .ToListAsync(ct);

        var assignees = members
            .Where(m => trainingMemberIds.Contains(m.Id))
            .OrderBy(m => m.Id)
            .ToList();

        var assignments = new List<WorkoutProgram>();
        foreach (var member in assignees)
        {
            var spec = templates[Math.Abs(member.Id) % templates.Count];
            var coach = trainers[Math.Abs(member.Id + 3) % trainers.Count];
            // Started somewhere inside the current block so "week 3 of 8" is a live number.
            var startsOn = today.AddDays(-rng.Next(6, spec.Weeks * 7 - 6));

            assignments.Add(Build(spec, exercises, member.Id, coach.Id, isTemplate: false,
                status: ProgramStatus.Published, startsOn, today));
        }

        db.WorkoutPrograms.AddRange(assignments);
        await db.SaveChangesAsync(ct);
    }

    private static WorkoutProgram Build(
        TemplateSpec spec, IReadOnlyDictionary<string, Exercise> exercises,
        int? memberId, int trainerId, bool isTemplate, ProgramStatus status, DateOnly? startsOn, DateOnly today)
    {
        var program = new WorkoutProgram
        {
            Name = spec.Name,
            Description = spec.Description,
            Goal = spec.Goal,
            MemberId = memberId,
            TrainerId = trainerId,
            IsTemplate = isTemplate,
            Status = status,
            Author = PlanAuthor.Trainer,
            DurationWeeks = spec.Weeks,
            DaysPerWeek = spec.DaysPerWeek,
            StartsOn = startsOn,
            EndsOn = startsOn?.AddDays(spec.Weeks * 7),
            ApprovedBy = status == ProgramStatus.Published ? "Seed" : null,
            ApprovedAtUtc = status == ProgramStatus.Published ? today.ToDateTime(TimeOnly.MinValue) : null
        };

        for (var i = 0; i < spec.Days.Length; i++)
        {
            var day = spec.Days[i];
            var programDay = new ProgramDay
            {
                DayIndex = i + 1,
                Title = day.Title,
                Focus = day.Focus,
                IsRestDay = day.Rest,
                Notes = day.Rest ? "Walk, stretch, sleep. Recovery is where the adaptation happens." : null
            };

            var order = 1;
            foreach (var (name, sets, reps, rest, superset) in day.Work)
            {
                if (!exercises.TryGetValue(name, out var exercise)) continue;
                programDay.Exercises.Add(new ProgramExercise
                {
                    ExerciseId = exercise.Id,
                    OrderIndex = order++,
                    Sets = sets,
                    RepScheme = reps,
                    RestSeconds = rest,
                    SupersetGroup = superset
                });
            }

            program.Days.Add(programDay);
        }

        return program;
    }

    /// <summary>
    /// Four programmes written the way a coach would write them — named lifts, real rep
    /// schemes, supersets grouped by letter, and a rest day that says what to do on it.
    /// </summary>
    private static List<TemplateSpec> Templates() => new()
    {
        new TemplateSpec(
            "Foundation Strength — 4 Day",
            "Build strength",
            "Upper/lower split for the first serious strength block. Two heavy days, two volume days, everything else negotiable.",
            8, 4,
            new[]
            {
                new DaySpec("Lower — Heavy", "Squat focus", false, new (string, int, string, int, string?)[]
                {
                    ("Back Squat", 5, "5", 180, null),
                    ("Romanian Deadlift", 3, "8", 120, null),
                    ("Bulgarian Split Squat", 3, "10 each", 90, "A"),
                    ("Pallof Press", 3, "12 each", 60, "A")
                }),
                new DaySpec("Upper — Heavy", "Press and pull", false, new (string, int, string, int, string?)[]
                {
                    ("Barbell Bench Press", 5, "5", 180, null),
                    ("Barbell Row", 4, "8", 120, null),
                    ("Overhead Press", 3, "8", 120, null),
                    ("Face Pull", 3, "15", 60, "B"),
                    ("Barbell Curl", 3, "12", 60, "B")
                }),
                new DaySpec("Rest", "Walk and mobilise", true, Array.Empty<(string, int, string, int, string?)>()),
                new DaySpec("Lower — Volume", "Hinge focus", false, new (string, int, string, int, string?)[]
                {
                    ("Conventional Deadlift", 4, "6", 180, null),
                    ("Hip Thrust", 4, "10", 90, null),
                    ("Walking Lunge", 3, "20 steps", 90, null),
                    ("Hanging Leg Raise", 3, "12", 60, null)
                }),
                new DaySpec("Upper — Volume", "Hypertrophy", false, new (string, int, string, int, string?)[]
                {
                    ("Dumbbell Incline Press", 4, "10", 90, null),
                    ("Lat Pulldown", 4, "12", 75, null),
                    ("Seated Cable Row", 3, "12", 75, null),
                    ("Lateral Raise", 3, "15", 45, "C"),
                    ("Triceps Rope Extension", 3, "15", 45, "C")
                })
            }),

        new TemplateSpec(
            "Lean Recomposition — 3 Day",
            "Lose fat",
            "Full-body lifting three times a week with conditioning finishers. Built for members training around a desk job.",
            6, 3,
            new[]
            {
                new DaySpec("Full Body A", "Squat and press", false, new (string, int, string, int, string?)[]
                {
                    ("Back Squat", 4, "8", 120, null),
                    ("Dumbbell Incline Press", 3, "12", 75, "A"),
                    ("Seated Cable Row", 3, "12", 75, "A"),
                    ("Kettlebell Swing", 4, "15", 60, null),
                    ("Assault Bike Interval", 6, "30s hard / 60s easy", 60, null)
                }),
                new DaySpec("Full Body B", "Hinge and pull", false, new (string, int, string, int, string?)[]
                {
                    ("Romanian Deadlift", 4, "10", 120, null),
                    ("Lat Pulldown", 3, "12", 75, "B"),
                    ("Overhead Press", 3, "10", 75, "B"),
                    ("Farmer's Carry", 4, "40 m", 90, null),
                    ("Concept2 Row 500m", 4, "500 m", 120, null)
                }),
                new DaySpec("Full Body C", "Unilateral and core", false, new (string, int, string, int, string?)[]
                {
                    ("Bulgarian Split Squat", 4, "10 each", 90, null),
                    ("Barbell Row", 3, "10", 90, null),
                    ("Hip Thrust", 3, "12", 75, null),
                    ("Dead Bug", 3, "10 each", 45, "C"),
                    ("Plank", 3, "45s", 45, "C"),
                    ("Ski-Erg Intervals", 5, "45s / 45s", 45, null)
                })
            }),

        new TemplateSpec(
            "Hybrid Athlete — 5 Day",
            "Strength and conditioning",
            "Strength Monday to Thursday, engine work on Friday. For members who lift and still want to run a 10K.",
            10, 5,
            new[]
            {
                new DaySpec("Squat Day", "Max effort lower", false, new (string, int, string, int, string?)[]
                {
                    ("Front Squat", 5, "3", 180, null),
                    ("Leg Press", 4, "10", 90, null),
                    ("90/90 Hip Switch", 2, "8 each", 45, null)
                }),
                new DaySpec("Bench Day", "Max effort upper", false, new (string, int, string, int, string?)[]
                {
                    ("Barbell Bench Press", 5, "3", 180, null),
                    ("Weighted Pull-Up", 4, "6", 150, null),
                    ("Lateral Raise", 3, "15", 45, null)
                }),
                new DaySpec("Engine", "Aerobic base", false, new (string, int, string, int, string?)[]
                {
                    ("Concept2 Row 500m", 6, "500 m", 90, null),
                    ("Sled Push", 5, "20 m", 90, null),
                    ("Thoracic Bridge", 2, "8 each", 45, null)
                }),
                new DaySpec("Deadlift Day", "Posterior chain", false, new (string, int, string, int, string?)[]
                {
                    ("Conventional Deadlift", 5, "3", 210, null),
                    ("Barbell Row", 4, "8", 120, null),
                    ("Hanging Leg Raise", 3, "12", 60, null)
                }),
                new DaySpec("Olympic + Carry", "Power", false, new (string, int, string, int, string?)[]
                {
                    ("Clean & Jerk", 6, "2", 180, null),
                    ("Snatch", 5, "2", 180, null),
                    ("Farmer's Carry", 4, "40 m", 90, null),
                    ("Turkish Get-Up", 3, "3 each", 90, null)
                })
            }),

        new TemplateSpec(
            "Return to Training — 3 Day",
            "Get back in",
            "For members coming back after a long gap or an injury. Machines and dumbbells first, barbells when the movement looks right.",
            4, 3,
            new[]
            {
                new DaySpec("Reset A", "Movement quality", false, new (string, int, string, int, string?)[]
                {
                    ("Leg Press", 3, "12", 90, null),
                    ("Lat Pulldown", 3, "12", 75, null),
                    ("Dumbbell Incline Press", 3, "12", 75, null),
                    ("Dead Bug", 3, "8 each", 45, null)
                }),
                new DaySpec("Reset B", "Hinge pattern", false, new (string, int, string, int, string?)[]
                {
                    ("Romanian Deadlift", 3, "10", 90, null),
                    ("Seated Cable Row", 3, "12", 75, null),
                    ("Walking Lunge", 3, "16 steps", 75, null),
                    ("Pallof Press", 3, "10 each", 45, null)
                }),
                new DaySpec("Reset C", "Full body", false, new (string, int, string, int, string?)[]
                {
                    ("Back Squat", 3, "8", 120, null),
                    ("Overhead Press", 3, "10", 90, null),
                    ("Kettlebell Swing", 3, "12", 60, null),
                    ("Banded Ankle Dorsiflexion", 2, "10 each", 30, null),
                    ("Plank", 3, "30s", 45, null)
                })
            })
    };
}
