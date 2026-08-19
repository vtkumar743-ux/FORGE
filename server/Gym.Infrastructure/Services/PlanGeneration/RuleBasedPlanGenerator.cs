using Gym.Core.Enums;
using Gym.Core.Interfaces;

namespace Gym.Infrastructure.Services.PlanGeneration;

/// <summary>
/// The deterministic half of Module 4.2 — a coach's split written as rules. It is not a
/// degraded mode: with no API key configured this is what the gym runs on, so it has to
/// produce a programme a trainer would sign off on, from the same context object the model
/// gets. Same member, same day, same plan — which also makes the AI path reviewable, because
/// there is a baseline to compare a draft against.
/// </summary>
public class RuleBasedPlanGenerator : IPlanGenerator
{
    public string Engine => "rule-based";
    public bool IsLive => true;

    public Task<GeneratedWorkoutPlan> GenerateWorkoutAsync(PlanGenerationContext ctx, CancellationToken ct = default)
        => Task.FromResult(BuildWorkout(ctx));

    public Task<GeneratedDietPlan> GenerateDietAsync(PlanGenerationContext ctx, CancellationToken ct = default)
        => Task.FromResult(BuildDiet(ctx));

    // ---------------------------------------------------------------- workout

    public static GeneratedWorkoutPlan BuildWorkout(PlanGenerationContext ctx)
    {
        var goal = ClassifyGoal(ctx.Goal);
        var split = SplitFor(ctx.DaysPerWeek, goal);
        var injuries = (ctx.InjuryNotes ?? string.Empty).ToLowerInvariant();

        var days = new List<GeneratedWorkoutDay>();
        for (var i = 0; i < split.Count; i++)
        {
            var template = split[i];
            if (template.IsRest)
            {
                days.Add(new GeneratedWorkoutDay
                {
                    DayIndex = i + 1,
                    Title = template.Title,
                    Focus = "Recovery",
                    IsRestDay = true,
                    // A rest day that says nothing gets ignored; a rest day with a job gets done.
                    Notes = "Walk 6,000–8,000 steps and run through the mobility flow. No lifting."
                });
                continue;
            }

            var picks = new List<GeneratedWorkoutExercise>();
            var used = new HashSet<int>();
            var order = 0;

            foreach (var slot in template.Slots)
            {
                var exercise = Pick(ctx, slot, used, injuries);
                if (exercise is null) continue;
                used.Add(exercise.Id);

                var (sets, reps, rest) = Prescription(goal, slot.IsCompound, ctx.Level);
                picks.Add(new GeneratedWorkoutExercise
                {
                    ExerciseId = exercise.Id,
                    Sets = sets,
                    RepScheme = reps,
                    RestSeconds = rest,
                    // Start at a load the member has already proven, so week one is not a test week.
                    TargetWeightKg = SuggestLoad(ctx, exercise.Name, goal),
                    SupersetGroup = slot.SupersetGroup,
                    Notes = slot.Cue
                });
                order++;
            }

            days.Add(new GeneratedWorkoutDay
            {
                DayIndex = i + 1,
                Title = template.Title,
                Focus = template.Focus,
                IsRestDay = false,
                Notes = order == 0 ? "No matching equipment at this branch — a coach needs to swap these in by hand." : template.Notes,
                Exercises = picks
            });
        }

        var name = goal switch
        {
            GoalKind.FatLoss => "Lean Recomposition",
            GoalKind.Strength => "Foundation Strength",
            GoalKind.Muscle => "Hypertrophy Block",
            GoalKind.Endurance => "Conditioning Base",
            _ => "General Fitness"
        };

        return new GeneratedWorkoutPlan
        {
            Name = $"{name} · {ctx.DaysPerWeek}-day",
            Summary = SummaryFor(goal, ctx),
            Days = days
        };
    }

    private static string SummaryFor(GoalKind goal, PlanGenerationContext ctx)
    {
        var basis = ctx.SessionsLast30Days switch
        {
            0 => "Built for a return to training after a break",
            < 6 => "Built around a once-or-twice-a-week habit",
            < 14 => "Built on a steady three-a-week base",
            _ => "Built on a high training frequency"
        };
        var focus = goal switch
        {
            GoalKind.FatLoss => "compound lifts held heavy enough to keep muscle, with conditioning at the end so it never eats the session",
            GoalKind.Strength => "low-rep primary lifts with full rest, then accessories at a volume that will not interfere",
            GoalKind.Muscle => "moderate-rep volume across two exposures per muscle group a week",
            GoalKind.Endurance => "aerobic base work with enough strength to stay injury-free",
            _ => "a full-body rotation that covers every pattern twice a week"
        };
        var caveat = string.IsNullOrWhiteSpace(ctx.InjuryNotes)
            ? string.Empty
            : $" Movements loading the noted injury have been left out — a coach should confirm the substitutions.";
        return $"{basis}. {char.ToUpperInvariant(focus[0])}{focus[1..]}.{caveat}";
    }

    private static ExerciseOption? Pick(
        PlanGenerationContext ctx, Slot slot, HashSet<int> used, string injuries)
    {
        var candidates = ctx.ExerciseLibrary
            .Where(e => e.PrimaryMuscle == slot.Muscle)
            .Where(e => !used.Contains(e.Id))
            .Where(e => ctx.AvailableEquipment.Count == 0 || ctx.AvailableEquipment.Contains(e.Equipment))
            .Where(e => e.Level == ClassLevel.AllLevels || e.Level <= ctx.Level)
            .Where(e => !IsContraindicated(e, injuries))
            .ToList();

        if (candidates.Count == 0) return null;

        // Compound slots want the tracked barbell lifts; accessory slots want anything else.
        var ordered = slot.IsCompound
            ? candidates
                .OrderByDescending(e => e.IsStrengthTracked)
                .ThenBy(e => EquipmentRank(e.Equipment))
                .ThenBy(e => e.Id)
            : candidates
                .OrderBy(e => e.IsStrengthTracked)
                .ThenBy(e => EquipmentRank(e.Equipment))
                .ThenBy(e => e.Id);

        return ordered.First();
    }

    /// <summary>
    /// Deliberately conservative and deliberately readable: a keyword match a trainer can
    /// audit beats a clever rule nobody can check before it puts a bad bar on a bad shoulder.
    /// </summary>
    private static bool IsContraindicated(ExerciseOption e, string injuries)
    {
        if (injuries.Length == 0) return false;
        var name = e.Name.ToLowerInvariant();

        if (injuries.Contains("knee") && (name.Contains("squat") || name.Contains("lunge") || name.Contains("jump") || name.Contains("box"))) return true;
        if (injuries.Contains("shoulder") && (name.Contains("overhead") || name.Contains("press") || name.Contains("snatch") || name.Contains("jerk"))) return true;
        if (injuries.Contains("back") && (name.Contains("deadlift") || name.Contains("good morning") || name.Contains("bent-over") || name.Contains("bent over"))) return true;
        if (injuries.Contains("wrist") && (name.Contains("front rack") || name.Contains("clean") || name.Contains("push-up") || name.Contains("push up"))) return true;
        if (injuries.Contains("ankle") && (name.Contains("run") || name.Contains("jump") || name.Contains("skip"))) return true;
        return false;
    }

    private static int EquipmentRank(EquipmentKind kind) => kind switch
    {
        EquipmentKind.Barbell => 0,
        EquipmentKind.Dumbbell => 1,
        EquipmentKind.Machine => 2,
        EquipmentKind.Cable => 3,
        EquipmentKind.Kettlebell => 4,
        EquipmentKind.Bodyweight => 5,
        EquipmentKind.Bands => 6,
        _ => 7
    };

    private static (int Sets, string Reps, int RestSeconds) Prescription(GoalKind goal, bool compound, ClassLevel level)
    {
        var beginner = level is ClassLevel.Beginner or ClassLevel.AllLevels;
        return (goal, compound) switch
        {
            (GoalKind.Strength, true) => (beginner ? 4 : 5, beginner ? "5" : "3-5", 180),
            (GoalKind.Strength, false) => (3, "6-8", 120),
            (GoalKind.Muscle, true) => (4, "6-10", 120),
            (GoalKind.Muscle, false) => (3, "10-14", 75),
            (GoalKind.FatLoss, true) => (4, "8-10", 90),
            (GoalKind.FatLoss, false) => (3, "12-15", 45),
            (GoalKind.Endurance, true) => (3, "10-12", 75),
            (GoalKind.Endurance, false) => (3, "15-20", 45),
            (_, true) => (3, "8-12", 90),
            _ => (3, "10-12", 60)
        };
    }

    /// <summary>
    /// Working weight from the member's own best on that lift, not from a percentage table
    /// keyed to a 1RM they have never actually tested.
    /// </summary>
    private static decimal? SuggestLoad(PlanGenerationContext ctx, string exerciseName, GoalKind goal)
    {
        var best = ctx.RecentBests
            .FirstOrDefault(b => string.Equals(b.ExerciseName, exerciseName, StringComparison.OrdinalIgnoreCase));
        if (best is null || best.BestEstimatedOneRepMax <= 0) return null;

        var fraction = goal switch
        {
            GoalKind.Strength => 0.82m,
            GoalKind.Muscle => 0.72m,
            GoalKind.FatLoss => 0.68m,
            GoalKind.Endurance => 0.60m,
            _ => 0.70m
        };
        // Round to the plate maths an Indian gym floor actually has: 2.5 kg increments.
        var raw = best.BestEstimatedOneRepMax * fraction;
        return Math.Round(raw / 2.5m, MidpointRounding.AwayFromZero) * 2.5m;
    }

    // ---------------------------------------------------------------- diet

    public static GeneratedDietPlan BuildDiet(PlanGenerationContext ctx)
    {
        var goal = ClassifyGoal(ctx.Goal);
        var weight = ctx.WeightKg ?? 70m;
        var height = ctx.HeightCm ?? 170m;
        var age = ctx.Age ?? 30;

        // Mifflin-St Jeor, the same formula the public BMR calculator on the site uses —
        // a member who does the sums on the tools page must not get a different number here.
        var bmr = ctx.Gender == Gender.Female
            ? 10m * weight + 6.25m * height - 5m * age - 161m
            : 10m * weight + 6.25m * height - 5m * age + 5m;

        var activity = ctx.SessionsLast30Days switch
        {
            0 => 1.35m, < 6 => 1.45m, < 14 => 1.55m, < 22 => 1.7m, _ => 1.8m
        };
        var maintenance = bmr * activity;

        var target = goal switch
        {
            GoalKind.FatLoss => maintenance * 0.80m,
            GoalKind.Muscle => maintenance * 1.10m,
            GoalKind.Strength => maintenance * 1.05m,
            _ => maintenance
        };
        var calories = (int)(Math.Round(target / 25m) * 25m);

        // Protein per kg first — it is the number that decides whether the deficit costs muscle.
        var proteinPerKg = goal switch
        {
            GoalKind.FatLoss => 2.0m,
            GoalKind.Muscle => 1.8m,
            GoalKind.Strength => 1.8m,
            _ => 1.6m
        };
        // Vegetarian diets in this context lean on dal and paneer; the ceiling is practical, not ideological.
        if (ctx.IsVegetarian) proteinPerKg = Math.Min(proteinPerKg, 1.7m);

        var protein = (int)Math.Round(weight * proteinPerKg);
        var fat = (int)Math.Round(weight * (goal == GoalKind.FatLoss ? 0.8m : 0.9m));
        var carbCalories = calories - protein * 4 - fat * 9;
        var carbs = Math.Max(80, (int)Math.Round(carbCalories / 4m));

        var meals = BuildMeals(calories, protein, carbs, fat, ctx.IsVegetarian, goal);

        return new GeneratedDietPlan
        {
            Name = goal switch
            {
                GoalKind.FatLoss => "Cutting Plate",
                GoalKind.Muscle => "Mass Plate",
                GoalKind.Strength => "Strength Plate",
                _ => "Maintenance Plate"
            },
            Summary = $"{calories} kcal · {protein}g protein ({proteinPerKg:0.0} g/kg) · {carbs}g carbs · {fat}g fat, "
                      + $"from a {maintenance:N0} kcal maintenance estimate at {activity:0.00}x activity. "
                      + (ctx.IsVegetarian ? "Vegetarian throughout." : "Includes eggs, chicken and fish."),
            TargetCalories = calories,
            ProteinGrams = protein,
            CarbGrams = carbs,
            FatGrams = fat,
            Meals = meals
        };
    }

    private static IReadOnlyList<GeneratedMeal> BuildMeals(
        int calories, int protein, int carbs, int fat, bool vegetarian, GoalKind goal)
    {
        // Split by slot, then describe each slot in food people in Bengaluru actually eat —
        // a macro table nobody can shop for is a plan nobody follows.
        var split = new (MealSlot Slot, string Title, double Share, string Veg, string NonVeg, string Timing)[]
        {
            (MealSlot.Breakfast, "Breakfast", 0.25,
                "3 besan cheela + 150g curd + 6 almonds",
                "3 egg whites + 2 whole eggs bhurji + 2 multigrain toast",
                "Within an hour of waking"),
            (MealSlot.MidMorning, "Mid-morning", 0.10,
                "1 apple + 30g roasted chana",
                "1 apple + 30g roasted chana",
                "Around 11:30 AM"),
            (MealSlot.Lunch, "Lunch", 0.28,
                "2 roti + 1 katori rajma + 100g paneer bhurji + salad",
                "2 roti + 1 katori dal + 150g chicken curry + salad",
                "1–2 PM"),
            (MealSlot.PreWorkout, "Pre-workout", 0.09,
                "1 banana + black coffee",
                "1 banana + black coffee",
                "45 minutes before training"),
            (MealSlot.PostWorkout, "Post-workout", 0.10,
                "1 scoop whey in water + 1 date",
                "1 scoop whey in water + 1 date",
                "Within 30 minutes of the last set"),
            (MealSlot.Dinner, "Dinner", 0.18,
                "150g tofu / soya chunk sabzi + 1 roti + sauteed vegetables",
                "180g grilled fish or chicken + 1 roti + sauteed vegetables",
                "By 9 PM")
        };

        var meals = new List<GeneratedMeal>();
        var order = 0;
        foreach (var row in split)
        {
            // Post-workout and pre-workout are protein/carb heavy by design; the rest track the split.
            var slotProteinShare = row.Slot switch
            {
                MealSlot.PostWorkout => 0.18,
                MealSlot.PreWorkout => 0.04,
                MealSlot.MidMorning => 0.06,
                _ => row.Share
            };
            var slotFatShare = row.Slot switch
            {
                MealSlot.PostWorkout => 0.02,
                MealSlot.PreWorkout => 0.02,
                _ => row.Share * 1.1
            };

            meals.Add(new GeneratedMeal
            {
                Slot = row.Slot,
                Title = row.Title,
                Items = vegetarian ? row.Veg : row.NonVeg,
                Calories = (int)Math.Round(calories * row.Share),
                ProteinGrams = (int)Math.Round(protein * slotProteinShare),
                CarbGrams = (int)Math.Round(carbs * row.Share),
                FatGrams = (int)Math.Round(fat * slotFatShare),
                TimingHint = row.Timing
            });
            order++;
        }

        if (goal == GoalKind.FatLoss)
            meals.RemoveAll(m => m.Slot == MealSlot.MidMorning);

        return meals;
    }

    // ---------------------------------------------------------------- splits

    private enum GoalKind { General, FatLoss, Muscle, Strength, Endurance }

    private static GoalKind ClassifyGoal(string goal)
    {
        var g = goal.ToLowerInvariant();
        if (g.Contains("fat") || g.Contains("weight loss") || g.Contains("lean") || g.Contains("cut")) return GoalKind.FatLoss;
        if (g.Contains("strength") || g.Contains("powerlift") || g.Contains("heavy")) return GoalKind.Strength;
        if (g.Contains("muscle") || g.Contains("hypertroph") || g.Contains("size") || g.Contains("bulk")) return GoalKind.Muscle;
        if (g.Contains("endurance") || g.Contains("stamina") || g.Contains("run") || g.Contains("cardio")) return GoalKind.Endurance;
        return GoalKind.General;
    }

    private record Slot(MuscleGroup Muscle, bool IsCompound, string? SupersetGroup = null, string? Cue = null);

    private record DayTemplate(string Title, string? Focus, bool IsRest, IReadOnlyList<Slot> Slots, string? Notes = null);

    private static IReadOnlyList<DayTemplate> SplitFor(int daysPerWeek, GoalKind goal)
    {
        var days = Math.Clamp(daysPerWeek, 2, 6);

        DayTemplate Push() => new("Push", "Chest · Shoulders · Triceps", false, new[]
        {
            new Slot(MuscleGroup.Chest, true, null, "Two warm-up sets before the first working set."),
            new Slot(MuscleGroup.Shoulders, true),
            new Slot(MuscleGroup.Chest, false, "A"),
            new Slot(MuscleGroup.Arms, false, "A", "Superset with the chest movement above."),
            new Slot(MuscleGroup.Core, false)
        });

        DayTemplate Pull() => new("Pull", "Back · Biceps", false, new[]
        {
            new Slot(MuscleGroup.Back, true, null, "Full hang at the bottom of every rep."),
            new Slot(MuscleGroup.Back, false),
            new Slot(MuscleGroup.Arms, false, "A"),
            new Slot(MuscleGroup.Core, false, "A"),
            new Slot(MuscleGroup.Mobility, false)
        });

        DayTemplate Legs() => new("Legs", "Quads · Hamstrings · Glutes", false, new[]
        {
            new Slot(MuscleGroup.Legs, true, null, "Depth before load."),
            new Slot(MuscleGroup.Glutes, true),
            new Slot(MuscleGroup.Legs, false),
            new Slot(MuscleGroup.Core, false),
            new Slot(MuscleGroup.Cardio, false, null, "Finish with 8 minutes easy — nasal breathing only.")
        });

        DayTemplate Upper() => new("Upper Body", "Chest · Back · Shoulders · Arms", false, new[]
        {
            new Slot(MuscleGroup.Chest, true),
            new Slot(MuscleGroup.Back, true),
            new Slot(MuscleGroup.Shoulders, false, "A"),
            new Slot(MuscleGroup.Arms, false, "A"),
            new Slot(MuscleGroup.Core, false)
        });

        DayTemplate Lower() => new("Lower Body", "Legs · Glutes · Core", false, new[]
        {
            new Slot(MuscleGroup.Legs, true),
            new Slot(MuscleGroup.Glutes, true),
            new Slot(MuscleGroup.Legs, false),
            new Slot(MuscleGroup.Core, false),
            new Slot(MuscleGroup.Mobility, false)
        });

        DayTemplate FullBody(string title) => new(title, "Full body", false, new[]
        {
            new Slot(MuscleGroup.Legs, true),
            new Slot(MuscleGroup.Chest, true),
            new Slot(MuscleGroup.Back, true),
            new Slot(MuscleGroup.Core, false),
            new Slot(MuscleGroup.Cardio, false)
        });

        DayTemplate Conditioning() => new("Conditioning", "Engine", false, new[]
        {
            new Slot(MuscleGroup.Cardio, true, null, "Intervals: 5 rounds of 3 minutes hard, 2 minutes easy."),
            new Slot(MuscleGroup.FullBody, false),
            new Slot(MuscleGroup.Core, false),
            new Slot(MuscleGroup.Mobility, false)
        });

        DayTemplate Rest(string title) => new(title, null, true, Array.Empty<Slot>());

        return days switch
        {
            2 => new[] { FullBody("Full Body A"), Rest("Recovery"), FullBody("Full Body B") },
            3 => goal == GoalKind.Endurance
                ? new[] { FullBody("Full Body A"), Conditioning(), FullBody("Full Body B") }
                : new[] { FullBody("Full Body A"), FullBody("Full Body B"), FullBody("Full Body C") },
            4 => new[] { Upper(), Lower(), Rest("Recovery"), Upper(), Lower() },
            5 => new[] { Push(), Pull(), Legs(), Rest("Recovery"), Upper(), Lower() },
            _ => new[] { Push(), Pull(), Legs(), Push(), Pull(), Legs() }
        };
    }
}
