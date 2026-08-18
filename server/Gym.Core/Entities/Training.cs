using Gym.Core.Enums;

namespace Gym.Core.Entities;

public class Exercise : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public MuscleGroup PrimaryMuscle { get; set; }
    public string? SecondaryMuscles { get; set; }
    public EquipmentKind Equipment { get; set; }
    public ClassLevel Level { get; set; }
    public string? VideoUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Cues { get; set; }
    /// <summary>True for lifts the PR engine tracks by load (squat, bench, deadlift…).</summary>
    public bool IsStrengthTracked { get; set; }
    public bool IsActive { get; set; } = true;
}

public class WorkoutProgram : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? MemberId { get; set; }
    public Member? Member { get; set; }
    public int? TrainerId { get; set; }
    public Trainer? Trainer { get; set; }

    public ProgramStatus Status { get; set; } = ProgramStatus.Draft;
    public PlanAuthor Author { get; set; } = PlanAuthor.Trainer;
    /// <summary>Prompt/rules payload kept so an AI-generated draft is auditable and re-runnable.</summary>
    public string? GenerationContextJson { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    public int DurationWeeks { get; set; } = 4;
    public int DaysPerWeek { get; set; } = 4;
    public string? Goal { get; set; }
    public DateOnly? StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    /// <summary>Template programs are assignable in bulk and have no member.</summary>
    public bool IsTemplate { get; set; }

    public ICollection<ProgramDay> Days { get; set; } = new List<ProgramDay>();
}

public class ProgramDay : BaseEntity
{
    public int WorkoutProgramId { get; set; }
    public WorkoutProgram WorkoutProgram { get; set; } = null!;
    public int DayIndex { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Focus { get; set; }
    public bool IsRestDay { get; set; }
    public string? Notes { get; set; }

    public ICollection<ProgramExercise> Exercises { get; set; } = new List<ProgramExercise>();
}

public class ProgramExercise : BaseEntity
{
    public int ProgramDayId { get; set; }
    public ProgramDay ProgramDay { get; set; } = null!;
    public int ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;

    public int OrderIndex { get; set; }
    public int Sets { get; set; }
    public string RepScheme { get; set; } = "8-12";
    public int RestSeconds { get; set; } = 90;
    public decimal? TargetWeightKg { get; set; }
    public string? Tempo { get; set; }
    /// <summary>Groups exercises into a superset (same letter = same set block).</summary>
    public string? SupersetGroup { get; set; }
    public string? Notes { get; set; }
}

public class WorkoutLog : BaseEntity
{
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public int ExerciseId { get; set; }
    public Exercise Exercise { get; set; } = null!;
    public int? ProgramExerciseId { get; set; }
    public ProgramExercise? ProgramExercise { get; set; }

    public DateOnly PerformedOn { get; set; }
    public DateTime PerformedAtUtc { get; set; } = DateTime.UtcNow;
    public int SetNumber { get; set; }
    public int Reps { get; set; }
    public decimal WeightKg { get; set; }
    public int? Rpe { get; set; }
    public int? DurationSeconds { get; set; }
    public decimal? DistanceKm { get; set; }
    /// <summary>reps × weight — the number the strength chart plots.</summary>
    public decimal Volume { get; set; }
    /// <summary>Estimated 1RM (Epley) — how the PR engine compares sets of different reps.</summary>
    public decimal EstimatedOneRepMax { get; set; }
    public bool IsPersonalRecord { get; set; }
    public bool PrCelebrated { get; set; }
    public string? Notes { get; set; }
}

public class DietPlan : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int? MemberId { get; set; }
    public Member? Member { get; set; }
    public int? TrainerId { get; set; }
    public Trainer? Trainer { get; set; }

    public ProgramStatus Status { get; set; } = ProgramStatus.Draft;
    public PlanAuthor Author { get; set; } = PlanAuthor.Trainer;
    public string? GenerationContextJson { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    public int TargetCalories { get; set; }
    public int ProteinGrams { get; set; }
    public int CarbGrams { get; set; }
    public int FatGrams { get; set; }
    public string? Notes { get; set; }
    public bool IsVegetarian { get; set; }
    public DateOnly? StartsOn { get; set; }

    public ICollection<Meal> Meals { get; set; } = new List<Meal>();
}

public class Meal : BaseEntity
{
    public int DietPlanId { get; set; }
    public DietPlan DietPlan { get; set; } = null!;
    public MealSlot Slot { get; set; }
    public int OrderIndex { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>Indian food entries — "2 roti + 100g paneer bhurji + salad".</summary>
    public string Items { get; set; } = string.Empty;
    public int Calories { get; set; }
    public int ProteinGrams { get; set; }
    public int CarbGrams { get; set; }
    public int FatGrams { get; set; }
    public string? TimingHint { get; set; }
}

/// <summary>InBody-style composition entry — digitising what most Indian gyms keep on paper.</summary>
public class BodyScan : BaseEntity
{
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public DateOnly ScanDate { get; set; }

    public decimal WeightKg { get; set; }
    public decimal? BodyFatPercent { get; set; }
    public decimal? SkeletalMuscleMassKg { get; set; }
    public decimal? FatMassKg { get; set; }
    public decimal? VisceralFatLevel { get; set; }
    public decimal? Bmi { get; set; }
    public decimal? BasalMetabolicRate { get; set; }
    public decimal? TotalBodyWaterL { get; set; }
    public decimal? InBodyScore { get; set; }

    public decimal? ChestCm { get; set; }
    public decimal? WaistCm { get; set; }
    public decimal? HipCm { get; set; }
    public decimal? ThighCm { get; set; }
    public decimal? ArmCm { get; set; }

    public string? MeasuredBy { get; set; }
    public string? DeviceName { get; set; }
    public string? Notes { get; set; }
}

public class ProgressPhoto : BaseEntity
{
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public DateOnly TakenOn { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    /// <summary>front / side / back — drives the side-by-side compare view.</summary>
    public string Pose { get; set; } = "front";
    public decimal? WeightKg { get; set; }
    public bool IsPrivate { get; set; } = true;
    public string? Notes { get; set; }
}
