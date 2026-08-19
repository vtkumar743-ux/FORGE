using Gym.Core.Enums;

namespace Gym.Core.Interfaces;

/// <summary>
/// Everything the generator is allowed to know about a member. Assembled once by the
/// service layer so the rule-based path and the Claude path see identical inputs — which
/// is what makes the fallback a genuine substitute rather than a different feature.
/// </summary>
public record PlanGenerationContext
{
    public required int MemberId { get; init; }
    public required string FullName { get; init; }
    public int? Age { get; init; }
    public Gender Gender { get; init; }
    public decimal? HeightCm { get; init; }
    public decimal? WeightKg { get; init; }
    public decimal? BodyFatPercent { get; init; }
    public required string Goal { get; init; }
    /// <summary>Beginner / Intermediate / Advanced, inferred from training history when unset.</summary>
    public required ClassLevel Level { get; init; }
    public required int DaysPerWeek { get; init; }
    public required int DurationWeeks { get; init; }
    public string? InjuryNotes { get; init; }
    public string? MedicalNotes { get; init; }
    public bool IsVegetarian { get; init; }
    /// <summary>Equipment the member's home branch actually has — the plan may not name anything else.</summary>
    public required IReadOnlyList<EquipmentKind> AvailableEquipment { get; init; }
    /// <summary>The exercise library the plan must draw from: id, name, muscle, equipment.</summary>
    public required IReadOnlyList<ExerciseOption> ExerciseLibrary { get; init; }
    /// <summary>Recent training signal — sessions in the last 30 days and best e1RMs.</summary>
    public int SessionsLast30Days { get; init; }
    public IReadOnlyList<LiftHistory> RecentBests { get; init; } = Array.Empty<LiftHistory>();
    public string? TrainerNote { get; init; }
}

public record ExerciseOption(int Id, string Name, MuscleGroup PrimaryMuscle, EquipmentKind Equipment, ClassLevel Level, bool IsStrengthTracked);

public record LiftHistory(string ExerciseName, decimal BestEstimatedOneRepMax, DateOnly LastPerformedOn);

// ---------------------------------------------------------------- results

public record GeneratedWorkoutPlan
{
    public required string Name { get; init; }
    public required string Summary { get; init; }
    public required IReadOnlyList<GeneratedWorkoutDay> Days { get; init; }
}

public record GeneratedWorkoutDay
{
    public required int DayIndex { get; init; }
    public required string Title { get; init; }
    public string? Focus { get; init; }
    public bool IsRestDay { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<GeneratedWorkoutExercise> Exercises { get; init; } = Array.Empty<GeneratedWorkoutExercise>();
}

public record GeneratedWorkoutExercise
{
    public required int ExerciseId { get; init; }
    public required int Sets { get; init; }
    public required string RepScheme { get; init; }
    public int RestSeconds { get; init; } = 90;
    public decimal? TargetWeightKg { get; init; }
    public string? SupersetGroup { get; init; }
    public string? Notes { get; init; }
}

public record GeneratedDietPlan
{
    public required string Name { get; init; }
    public required string Summary { get; init; }
    public required int TargetCalories { get; init; }
    public required int ProteinGrams { get; init; }
    public required int CarbGrams { get; init; }
    public required int FatGrams { get; init; }
    public required IReadOnlyList<GeneratedMeal> Meals { get; init; }
}

public record GeneratedMeal
{
    public required MealSlot Slot { get; init; }
    public required string Title { get; init; }
    public required string Items { get; init; }
    public required int Calories { get; init; }
    public required int ProteinGrams { get; init; }
    public required int CarbGrams { get; init; }
    public required int FatGrams { get; init; }
    public string? TimingHint { get; init; }
}

/// <summary>
/// What actually produced the draft, carried onto the entity so the admin screen can say
/// "AI draft" or "rule-based draft" honestly — and say why, when the model was unavailable.
/// </summary>
public record PlanGenerationOutcome<T>(T Plan, string Engine, string? FallbackReason)
{
    public bool UsedAi => Engine.StartsWith("claude", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The seam Module 4.2 is built behind. Claude when a key is configured, a deterministic
/// rule-based programmer otherwise — the admin approves either one before a member sees it,
/// so the gym is never publishing a plan nobody read.
/// </summary>
public interface IPlanGenerator
{
    string Engine { get; }
    bool IsLive { get; }
    Task<GeneratedWorkoutPlan> GenerateWorkoutAsync(PlanGenerationContext context, CancellationToken ct = default);
    Task<GeneratedDietPlan> GenerateDietAsync(PlanGenerationContext context, CancellationToken ct = default);
}
