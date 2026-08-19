using System.Text.Json;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.Services.PlanGeneration;

/// <summary>
/// Module 4.2 end to end: assemble the member's context, ask whichever generator is
/// configured, and persist the answer as a <see cref="ProgramStatus.Draft"/> that no member
/// can see until an admin approves it. The fallback is inside this class rather than inside
/// the Claude client so a model outage is a logged sentence on the draft ("AI unavailable —
/// rule-based plan used"), not a failed request the desk has to interpret.
/// </summary>
public class PlanGenerationService
{
    private readonly GymDbContext _db;
    private readonly ClaudePlanGenerator _claude;
    private readonly RuleBasedPlanGenerator _rules;
    private readonly IClock _clock;
    private readonly ILogger<PlanGenerationService> _log;

    public PlanGenerationService(
        GymDbContext db, ClaudePlanGenerator claude, RuleBasedPlanGenerator rules,
        IClock clock, ILogger<PlanGenerationService> log)
    {
        _db = db;
        _claude = claude;
        _rules = rules;
        _clock = clock;
        _log = log;
    }

    public bool AiAvailable => _claude.IsLive;
    public string ActiveEngine => _claude.IsLive ? _claude.Engine : _rules.Engine;

    // ---------------------------------------------------------------- context

    public async Task<PlanGenerationContext?> BuildContextAsync(
        int memberId, PlanRequestOptions options, CancellationToken ct = default)
    {
        var member = await _db.Members.AsNoTracking()
            .Include(m => m.HomeBranch)
            .FirstOrDefaultAsync(m => m.Id == memberId, ct);
        if (member is null) return null;

        var today = _clock.Today;
        var since = _clock.UtcNow.AddDays(-30);

        var sessions = await _db.WorkoutLogs.AsNoTracking()
            .Where(l => l.MemberId == memberId && l.PerformedAtUtc >= since)
            .Select(l => l.PerformedOn)
            .Distinct()
            .CountAsync(ct);

        var checkIns = await _db.CheckIns.AsNoTracking()
            .CountAsync(c => c.MemberId == memberId && !c.WasBlocked && c.CheckInAtUtc >= since, ct);

        // Best e1RM per lift, computed with its own grouped query — a best-ever figure taken
        // off a capped history page can understate a record, and understating someone's own
        // best is the one number a generated plan must never get wrong.
        var bests = await _db.WorkoutLogs.AsNoTracking()
            .Where(l => l.MemberId == memberId && l.Exercise.IsStrengthTracked && l.EstimatedOneRepMax > 0)
            .GroupBy(l => l.Exercise.Name)
            .Select(g => new
            {
                ExerciseName = g.Key,
                Best = g.Max(x => x.EstimatedOneRepMax),
                Last = g.Max(x => x.PerformedOn)
            })
            .OrderByDescending(x => x.Best)
            .Take(12)
            .ToListAsync(ct);

        var latestScan = await _db.BodyScans.AsNoTracking()
            .Where(s => s.MemberId == memberId)
            .OrderByDescending(s => s.ScanDate)
            .FirstOrDefaultAsync(ct);

        var library = await _db.Exercises.AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.PrimaryMuscle).ThenBy(e => e.Name)
            .Select(e => new ExerciseOption(e.Id, e.Name, e.PrimaryMuscle, e.Equipment, e.Level, e.IsStrengthTracked))
            .ToListAsync(ct);

        var level = options.Level ?? (checkIns + sessions) switch
        {
            < 8 => ClassLevel.Beginner,
            < 20 => ClassLevel.Intermediate,
            _ => ClassLevel.Advanced
        };

        var age = member.DateOfBirth is { } dob
            ? Math.Max(14, today.Year - dob.Year - (today.DayOfYear < dob.DayOfYear ? 1 : 0))
            : (int?)null;

        return new PlanGenerationContext
        {
            MemberId = member.Id,
            FullName = member.FullName,
            Age = age,
            Gender = member.Gender,
            HeightCm = member.HeightCm,
            WeightKg = latestScan?.WeightKg ?? member.StartWeightKg,
            BodyFatPercent = latestScan?.BodyFatPercent,
            Goal = string.IsNullOrWhiteSpace(options.Goal)
                ? member.PrimaryGoal ?? "General fitness"
                : options.Goal.Trim(),
            Level = level,
            DaysPerWeek = Math.Clamp(options.DaysPerWeek ?? 4, 2, 6),
            DurationWeeks = Math.Clamp(options.DurationWeeks ?? 6, 2, 16),
            InjuryNotes = member.InjuryNotes,
            MedicalNotes = member.MedicalNotes,
            IsVegetarian = options.IsVegetarian,
            // The equipment list is the whole library's equipment for now: every branch is a
            // full-service floor. The seam is here for the day a studio branch opens.
            AvailableEquipment = library.Select(e => e.Equipment).Distinct().ToList(),
            ExerciseLibrary = library,
            SessionsLast30Days = Math.Max(sessions, checkIns),
            RecentBests = bests
                .Select(b => new LiftHistory(b.ExerciseName, Math.Round(b.Best, 1), b.Last))
                .ToList(),
            TrainerNote = options.TrainerNote
        };
    }

    // ---------------------------------------------------------------- generate

    public async Task<PlanGenerationOutcome<GeneratedWorkoutPlan>> GenerateWorkoutAsync(
        PlanGenerationContext ctx, CancellationToken ct = default)
    {
        if (_claude.IsLive)
        {
            try
            {
                var plan = await _claude.GenerateWorkoutAsync(ctx, ct);
                return new PlanGenerationOutcome<GeneratedWorkoutPlan>(plan, _claude.Engine, null);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Claude workout generation failed for member {MemberId}; using rules", ctx.MemberId);
                return new PlanGenerationOutcome<GeneratedWorkoutPlan>(
                    RuleBasedPlanGenerator.BuildWorkout(ctx), _rules.Engine, Describe(ex));
            }
        }

        return new PlanGenerationOutcome<GeneratedWorkoutPlan>(
            RuleBasedPlanGenerator.BuildWorkout(ctx), _rules.Engine,
            "No Anthropic API key configured.");
    }

    public async Task<PlanGenerationOutcome<GeneratedDietPlan>> GenerateDietAsync(
        PlanGenerationContext ctx, CancellationToken ct = default)
    {
        if (_claude.IsLive)
        {
            try
            {
                var plan = await _claude.GenerateDietAsync(ctx, ct);
                return new PlanGenerationOutcome<GeneratedDietPlan>(plan, _claude.Engine, null);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Claude diet generation failed for member {MemberId}; using rules", ctx.MemberId);
                return new PlanGenerationOutcome<GeneratedDietPlan>(
                    RuleBasedPlanGenerator.BuildDiet(ctx), _rules.Engine, Describe(ex));
            }
        }

        return new PlanGenerationOutcome<GeneratedDietPlan>(
            RuleBasedPlanGenerator.BuildDiet(ctx), _rules.Engine,
            "No Anthropic API key configured.");
    }

    private static string Describe(Exception ex) =>
        ex is TaskCanceledException or TimeoutException
            ? "The model did not answer in time."
            : ex.Message.Length > 240 ? ex.Message[..240] : ex.Message;

    // ---------------------------------------------------------------- persist

    public async Task<WorkoutProgram> SaveWorkoutDraftAsync(
        PlanGenerationContext ctx, PlanGenerationOutcome<GeneratedWorkoutPlan> outcome,
        int? trainerId, CancellationToken ct = default)
    {
        var program = new WorkoutProgram
        {
            Name = outcome.Plan.Name,
            Description = outcome.Plan.Summary,
            MemberId = ctx.MemberId,
            TrainerId = trainerId,
            Status = ProgramStatus.Draft,
            Author = outcome.UsedAi ? PlanAuthor.Ai : PlanAuthor.Admin,
            GenerationContextJson = Audit(ctx, outcome.Engine, outcome.FallbackReason),
            DurationWeeks = ctx.DurationWeeks,
            DaysPerWeek = ctx.DaysPerWeek,
            Goal = ctx.Goal,
            IsTemplate = false
        };

        foreach (var day in outcome.Plan.Days)
        {
            var entity = new ProgramDay
            {
                DayIndex = day.DayIndex,
                Title = day.Title,
                Focus = day.Focus,
                IsRestDay = day.IsRestDay,
                Notes = day.Notes
            };
            var order = 0;
            foreach (var exercise in day.Exercises)
            {
                entity.Exercises.Add(new ProgramExercise
                {
                    ExerciseId = exercise.ExerciseId,
                    OrderIndex = order++,
                    Sets = exercise.Sets,
                    RepScheme = exercise.RepScheme,
                    RestSeconds = exercise.RestSeconds,
                    TargetWeightKg = exercise.TargetWeightKg,
                    SupersetGroup = exercise.SupersetGroup,
                    Notes = exercise.Notes
                });
            }
            program.Days.Add(entity);
        }

        _db.WorkoutPrograms.Add(program);
        await _db.SaveChangesAsync(ct);
        return program;
    }

    public async Task<DietPlan> SaveDietDraftAsync(
        PlanGenerationContext ctx, PlanGenerationOutcome<GeneratedDietPlan> outcome,
        int? trainerId, CancellationToken ct = default)
    {
        var plan = new DietPlan
        {
            Name = outcome.Plan.Name,
            MemberId = ctx.MemberId,
            TrainerId = trainerId,
            Status = ProgramStatus.Draft,
            Author = outcome.UsedAi ? PlanAuthor.Ai : PlanAuthor.Admin,
            GenerationContextJson = Audit(ctx, outcome.Engine, outcome.FallbackReason),
            TargetCalories = outcome.Plan.TargetCalories,
            ProteinGrams = outcome.Plan.ProteinGrams,
            CarbGrams = outcome.Plan.CarbGrams,
            FatGrams = outcome.Plan.FatGrams,
            Notes = outcome.Plan.Summary,
            IsVegetarian = ctx.IsVegetarian,
            StartsOn = _clock.Today
        };

        var order = 0;
        foreach (var meal in outcome.Plan.Meals.OrderBy(m => m.Slot))
        {
            plan.Meals.Add(new Meal
            {
                Slot = meal.Slot,
                OrderIndex = order++,
                Title = meal.Title,
                Items = meal.Items,
                Calories = meal.Calories,
                ProteinGrams = meal.ProteinGrams,
                CarbGrams = meal.CarbGrams,
                FatGrams = meal.FatGrams,
                TimingHint = meal.TimingHint
            });
        }

        _db.DietPlans.Add(plan);
        await _db.SaveChangesAsync(ct);
        return plan;
    }

    /// <summary>
    /// The inputs, the engine and any fallback reason, stored on the draft. A plan a member
    /// is following has to be answerable a year later: what did we know, and who decided.
    /// </summary>
    private string Audit(PlanGenerationContext ctx, string engine, string? fallbackReason) =>
        JsonSerializer.Serialize(new
        {
            engine,
            fallbackReason,
            generatedAtUtc = _clock.UtcNow,
            input = new
            {
                ctx.Goal,
                level = ctx.Level.ToString(),
                ctx.DaysPerWeek,
                ctx.DurationWeeks,
                ctx.Age,
                ctx.HeightCm,
                ctx.WeightKg,
                ctx.BodyFatPercent,
                ctx.IsVegetarian,
                ctx.InjuryNotes,
                ctx.SessionsLast30Days,
                bests = ctx.RecentBests.Select(b => new { b.ExerciseName, b.BestEstimatedOneRepMax }),
                ctx.TrainerNote
            }
        });
}

public record PlanRequestOptions
{
    public string? Goal { get; init; }
    public ClassLevel? Level { get; init; }
    public int? DaysPerWeek { get; init; }
    public int? DurationWeeks { get; init; }
    public bool IsVegetarian { get; init; }
    public string? TrainerNote { get; init; }
    public int? TrainerId { get; init; }
}
