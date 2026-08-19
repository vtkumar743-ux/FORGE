using System.Text.Json;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Services.PlanGeneration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers.Admin;

/// <summary>
/// The plan studio (Module 4.2): generate a workout or diet draft for a member, review it,
/// edit it, then publish. Nothing generated is visible to a member until an admin publishes
/// it — the generator writes a <c>Draft</c> and only this controller can move it.
/// </summary>
[ApiController]
[Route("api/admin/plan-studio")]
[Authorize(Roles = RoleNames.Admin)]
[Produces("application/json")]
public class AdminPlanStudioController : ControllerBase
{
    private readonly GymDbContext _db;
    private readonly PlanGenerationService _generator;
    private readonly INotificationDispatcher _notifier;
    private readonly IClock _clock;
    private readonly ILogger<AdminPlanStudioController> _log;

    public AdminPlanStudioController(
        GymDbContext db, PlanGenerationService generator, INotificationDispatcher notifier,
        IClock clock, ILogger<AdminPlanStudioController> log)
    {
        _db = db;
        _generator = generator;
        _notifier = notifier;
        _clock = clock;
        _log = log;
    }

    /// <summary>Which engine is live, so the UI can say so before the owner presses Generate.</summary>
    [HttpGet("engine")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult Engine() => Ok(new
    {
        engine = _generator.ActiveEngine,
        aiAvailable = _generator.AiAvailable,
        description = _generator.AiAvailable
            ? "Claude writes the draft; a rule-based programmer stands in if the model is unreachable."
            : "No Anthropic API key is configured, so drafts come from the rule-based programmer. Add Anthropic:ApiKey to switch it on."
    });

    // ================================================================== generate

    [HttpPost("workout/{memberId:int}")]
    [ProducesResponseType(typeof(WorkoutDraftResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkoutDraftResponse>> GenerateWorkout(
        int memberId, GeneratePlanRequest request, CancellationToken ct)
    {
        var context = await _generator.BuildContextAsync(memberId, ToOptions(request), ct);
        if (context is null) return NotFound();

        var outcome = await _generator.GenerateWorkoutAsync(context, ct);
        var program = await _generator.SaveWorkoutDraftAsync(context, outcome, request.TrainerId, ct);

        _log.LogInformation("Workout draft {ProgramId} generated for member {MemberId} by {Engine}",
            program.Id, memberId, outcome.Engine);

        var response = await LoadWorkoutAsync(program.Id, ct);
        return CreatedAtAction(nameof(Workout), new { id = program.Id }, response);
    }

    [HttpPost("diet/{memberId:int}")]
    [ProducesResponseType(typeof(DietDraftResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DietDraftResponse>> GenerateDiet(
        int memberId, GeneratePlanRequest request, CancellationToken ct)
    {
        var context = await _generator.BuildContextAsync(memberId, ToOptions(request), ct);
        if (context is null) return NotFound();

        var outcome = await _generator.GenerateDietAsync(context, ct);
        var plan = await _generator.SaveDietDraftAsync(context, outcome, request.TrainerId, ct);

        var response = await LoadDietAsync(plan.Id, ct);
        return CreatedAtAction(nameof(Diet), new { id = plan.Id }, response);
    }

    // ================================================================== read

    /// <summary>Everything awaiting review, newest first — the queue the owner works through.</summary>
    [HttpGet("queue")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Queue([FromQuery] int? memberId, CancellationToken ct)
    {
        var workouts = await _db.WorkoutPrograms.AsNoTracking()
            .Where(p => !p.IsTemplate && p.MemberId != null)
            .Where(p => memberId == null || p.MemberId == memberId)
            .Where(p => p.Status == ProgramStatus.Draft || p.Status == ProgramStatus.PendingApproval)
            .OrderByDescending(p => p.Id)
            .Take(50)
            .Select(p => new
            {
                p.Id, p.Name, p.Status, p.Author, p.Goal, p.DaysPerWeek, p.DurationWeeks,
                MemberId = p.MemberId!.Value, MemberName = p.Member!.FullName, MemberCode = p.Member.MemberCode,
                p.CreatedAtUtc, Days = p.Days.Count
            })
            .ToListAsync(ct);

        var diets = await _db.DietPlans.AsNoTracking()
            .Where(p => p.MemberId != null)
            .Where(p => memberId == null || p.MemberId == memberId)
            .Where(p => p.Status == ProgramStatus.Draft || p.Status == ProgramStatus.PendingApproval)
            .OrderByDescending(p => p.Id)
            .Take(50)
            .Select(p => new
            {
                p.Id, p.Name, p.Status, p.Author, p.TargetCalories, p.ProteinGrams,
                MemberId = p.MemberId!.Value, MemberName = p.Member!.FullName, MemberCode = p.Member.MemberCode,
                p.CreatedAtUtc, Meals = p.Meals.Count
            })
            .ToListAsync(ct);

        return Ok(new { workouts, diets });
    }

    [HttpGet("workout/{id:int}")]
    [ProducesResponseType(typeof(WorkoutDraftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkoutDraftResponse>> Workout(int id, CancellationToken ct)
    {
        var response = await LoadWorkoutAsync(id, ct);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("diet/{id:int}")]
    [ProducesResponseType(typeof(DietDraftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DietDraftResponse>> Diet(int id, CancellationToken ct)
    {
        var response = await LoadDietAsync(id, ct);
        return response is null ? NotFound() : Ok(response);
    }

    // ================================================================== edit

    /// <summary>
    /// The review edit: the fields a coach actually changes on a draft — sets, reps, rest,
    /// target load and the note. Swapping the exercise itself means regenerating or building
    /// it by hand, which is the honest boundary for a review screen.
    /// </summary>
    [HttpPut("workout/{id:int}")]
    [ProducesResponseType(typeof(WorkoutDraftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkoutDraftResponse>> UpdateWorkout(
        int id, UpdateWorkoutDraftRequest request, CancellationToken ct)
    {
        var program = await _db.WorkoutPrograms
            .Include(p => p.Days).ThenInclude(d => d.Exercises)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (program is null) return NotFound();
        if (program.Status == ProgramStatus.Published)
            return Conflict(new ProblemDetails
            {
                Title = "Already published",
                Detail = "A member is following this plan. Generate a new draft rather than editing it under them."
            });

        if (!string.IsNullOrWhiteSpace(request.Name)) program.Name = request.Name.Trim();
        if (request.Description is not null) program.Description = request.Description.Trim();
        if (request.Goal is not null) program.Goal = request.Goal.Trim();
        if (request.DurationWeeks is { } weeks) program.DurationWeeks = Math.Clamp(weeks, 1, 24);

        foreach (var dayEdit in request.Days ?? Array.Empty<UpdateProgramDay>())
        {
            var day = program.Days.FirstOrDefault(d => d.Id == dayEdit.Id);
            if (day is null) continue;
            if (!string.IsNullOrWhiteSpace(dayEdit.Title)) day.Title = dayEdit.Title.Trim();
            if (dayEdit.Focus is not null) day.Focus = dayEdit.Focus.Trim();
            if (dayEdit.Notes is not null) day.Notes = dayEdit.Notes.Trim();

            foreach (var exEdit in dayEdit.Exercises ?? Array.Empty<UpdateProgramExercise>())
            {
                var exercise = day.Exercises.FirstOrDefault(e => e.Id == exEdit.Id);
                if (exercise is null) continue;
                if (exEdit.Remove)
                {
                    _db.Remove(exercise);
                    continue;
                }
                if (exEdit.Sets is { } sets) exercise.Sets = Math.Clamp(sets, 1, 10);
                if (!string.IsNullOrWhiteSpace(exEdit.RepScheme)) exercise.RepScheme = exEdit.RepScheme.Trim();
                if (exEdit.RestSeconds is { } rest) exercise.RestSeconds = Math.Clamp(rest, 15, 600);
                if (exEdit.TargetWeightKg is not null) exercise.TargetWeightKg = exEdit.TargetWeightKg;
                if (exEdit.Notes is not null) exercise.Notes = exEdit.Notes.Trim();
            }
        }

        await _db.SaveChangesAsync(ct);
        return Ok((await LoadWorkoutAsync(id, ct))!);
    }

    [HttpPut("diet/{id:int}")]
    [ProducesResponseType(typeof(DietDraftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DietDraftResponse>> UpdateDiet(
        int id, UpdateDietDraftRequest request, CancellationToken ct)
    {
        var plan = await _db.DietPlans.Include(p => p.Meals).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return NotFound();
        if (plan.Status == ProgramStatus.Published)
            return Conflict(new ProblemDetails { Title = "Already published" });

        if (!string.IsNullOrWhiteSpace(request.Name)) plan.Name = request.Name.Trim();
        if (request.Notes is not null) plan.Notes = request.Notes.Trim();
        if (request.TargetCalories is { } cal) plan.TargetCalories = Math.Clamp(cal, 800, 6000);
        if (request.ProteinGrams is { } p) plan.ProteinGrams = Math.Clamp(p, 20, 400);
        if (request.CarbGrams is { } c) plan.CarbGrams = Math.Clamp(c, 20, 800);
        if (request.FatGrams is { } f) plan.FatGrams = Math.Clamp(f, 10, 300);

        foreach (var mealEdit in request.Meals ?? Array.Empty<UpdateMeal>())
        {
            var meal = plan.Meals.FirstOrDefault(m => m.Id == mealEdit.Id);
            if (meal is null) continue;
            if (mealEdit.Remove) { _db.Remove(meal); continue; }
            if (!string.IsNullOrWhiteSpace(mealEdit.Title)) meal.Title = mealEdit.Title.Trim();
            if (!string.IsNullOrWhiteSpace(mealEdit.Items)) meal.Items = mealEdit.Items.Trim();
            if (mealEdit.Calories is { } mc) meal.Calories = Math.Max(0, mc);
            if (mealEdit.ProteinGrams is { } mp) meal.ProteinGrams = Math.Max(0, mp);
            if (mealEdit.CarbGrams is { } mcarb) meal.CarbGrams = Math.Max(0, mcarb);
            if (mealEdit.FatGrams is { } mf) meal.FatGrams = Math.Max(0, mf);
            if (mealEdit.TimingHint is not null) meal.TimingHint = mealEdit.TimingHint.Trim();
        }

        await _db.SaveChangesAsync(ct);
        return Ok((await LoadDietAsync(id, ct))!);
    }

    // ================================================================== publish

    /// <summary>
    /// Publishing is the approval. It stamps who approved it and when, retires whatever the
    /// member was following, and tells them — a plan that appears silently is a plan nobody starts.
    /// </summary>
    [HttpPost("workout/{id:int}/publish")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PublishWorkout(int id, CancellationToken ct)
    {
        var program = await _db.WorkoutPrograms
            .Include(p => p.Days)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (program is null || program.MemberId is null) return NotFound();

        if (program.Days.Count == 0)
            return BadRequest(new ProblemDetails { Title = "Nothing to publish", Detail = "This draft has no training days." });

        var others = await _db.WorkoutPrograms
            .Where(p => p.MemberId == program.MemberId && p.Id != program.Id && p.Status == ProgramStatus.Published)
            .ToListAsync(ct);
        foreach (var other in others) other.Status = ProgramStatus.Archived;

        program.Status = ProgramStatus.Published;
        program.ApprovedBy = User.Identity?.Name ?? "admin";
        program.ApprovedAtUtc = _clock.UtcNow;
        program.StartsOn ??= _clock.Today;
        program.EndsOn = program.StartsOn.Value.AddDays(program.DurationWeeks * 7);

        await _db.SaveChangesAsync(ct);

        await _notifier.SendAsync(new OutboundMessage
        {
            MemberId = program.MemberId,
            Kind = NotificationKind.General,
            Title = "Your new programme is ready",
            Body = $"{program.Name} — {program.DaysPerWeek} days a week for {program.DurationWeeks} weeks. Open it and log your first session.",
            ActionUrl = "/portal/workouts",
            TemplateKey = "plan.published",
            Channels = new[] { NotificationChannel.InApp, NotificationChannel.WhatsApp }
        }, ct);

        return Ok(new { published = true, program.Id, program.ApprovedBy, program.ApprovedAtUtc, archived = others.Count });
    }

    [HttpPost("diet/{id:int}/publish")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PublishDiet(int id, CancellationToken ct)
    {
        var plan = await _db.DietPlans.Include(p => p.Meals).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null || plan.MemberId is null) return NotFound();
        if (plan.Meals.Count == 0)
            return BadRequest(new ProblemDetails { Title = "Nothing to publish", Detail = "This draft has no meals." });

        var others = await _db.DietPlans
            .Where(p => p.MemberId == plan.MemberId && p.Id != plan.Id && p.Status == ProgramStatus.Published)
            .ToListAsync(ct);
        foreach (var other in others) other.Status = ProgramStatus.Archived;

        plan.Status = ProgramStatus.Published;
        plan.ApprovedBy = User.Identity?.Name ?? "admin";
        plan.ApprovedAtUtc = _clock.UtcNow;
        plan.StartsOn ??= _clock.Today;

        await _db.SaveChangesAsync(ct);

        await _notifier.SendAsync(new OutboundMessage
        {
            MemberId = plan.MemberId,
            Kind = NotificationKind.General,
            Title = "Your eating plan is ready",
            Body = $"{plan.Name} — {plan.TargetCalories} kcal with {plan.ProteinGrams}g protein a day.",
            ActionUrl = "/portal/workouts",
            TemplateKey = "diet.published",
            Channels = new[] { NotificationChannel.InApp, NotificationChannel.WhatsApp }
        }, ct);

        return Ok(new { published = true, plan.Id, plan.ApprovedBy, plan.ApprovedAtUtc, archived = others.Count });
    }

    [HttpDelete("workout/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DiscardWorkout(int id, CancellationToken ct)
    {
        var program = await _db.WorkoutPrograms.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (program is null) return NotFound();
        if (program.Status == ProgramStatus.Published)
            return Conflict(new ProblemDetails { Title = "Published plans are archived, not deleted." });
        _db.Remove(program);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("diet/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DiscardDiet(int id, CancellationToken ct)
    {
        var plan = await _db.DietPlans.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return NotFound();
        if (plan.Status == ProgramStatus.Published)
            return Conflict(new ProblemDetails { Title = "Published plans are archived, not deleted." });
        _db.Remove(plan);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ================================================================== mapping

    private static PlanRequestOptions ToOptions(GeneratePlanRequest request) => new()
    {
        Goal = request.Goal,
        Level = request.Level,
        DaysPerWeek = request.DaysPerWeek,
        DurationWeeks = request.DurationWeeks,
        IsVegetarian = request.IsVegetarian,
        TrainerNote = request.TrainerNote,
        TrainerId = request.TrainerId
    };

    private async Task<WorkoutDraftResponse?> LoadWorkoutAsync(int id, CancellationToken ct)
    {
        var program = await _db.WorkoutPrograms.AsNoTracking()
            .Include(p => p.Member)
            .Include(p => p.Trainer)
            .Include(p => p.Days).ThenInclude(d => d.Exercises).ThenInclude(e => e.Exercise)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (program is null) return null;

        var audit = ParseAudit(program.GenerationContextJson);

        return new WorkoutDraftResponse
        {
            Id = program.Id,
            Name = program.Name,
            Description = program.Description,
            Status = program.Status,
            Author = program.Author,
            AuthorLabel = AuthorLabel(program.Author, program.ApprovedBy),
            Engine = audit.Engine,
            FallbackReason = audit.FallbackReason,
            Goal = program.Goal,
            DaysPerWeek = program.DaysPerWeek,
            DurationWeeks = program.DurationWeeks,
            MemberId = program.MemberId,
            MemberName = program.Member?.FullName,
            MemberCode = program.Member?.MemberCode,
            TrainerName = program.Trainer?.FullName,
            ApprovedBy = program.ApprovedBy,
            ApprovedAtUtc = program.ApprovedAtUtc,
            CreatedAtUtc = program.CreatedAtUtc,
            Days = program.Days.OrderBy(d => d.DayIndex).Select(d => new WorkoutDraftDay
            {
                Id = d.Id,
                DayIndex = d.DayIndex,
                Title = d.Title,
                Focus = d.Focus,
                IsRestDay = d.IsRestDay,
                Notes = d.Notes,
                Exercises = d.Exercises.OrderBy(e => e.OrderIndex).Select(e => new WorkoutDraftExercise
                {
                    Id = e.Id,
                    ExerciseId = e.ExerciseId,
                    Name = e.Exercise.Name,
                    PrimaryMuscle = e.Exercise.PrimaryMuscle.ToString(),
                    Equipment = e.Exercise.Equipment.ToString(),
                    Sets = e.Sets,
                    RepScheme = e.RepScheme,
                    RestSeconds = e.RestSeconds,
                    TargetWeightKg = e.TargetWeightKg,
                    SupersetGroup = e.SupersetGroup,
                    Notes = e.Notes
                }).ToList()
            }).ToList()
        };
    }

    private async Task<DietDraftResponse?> LoadDietAsync(int id, CancellationToken ct)
    {
        var plan = await _db.DietPlans.AsNoTracking()
            .Include(p => p.Member)
            .Include(p => p.Meals)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return null;

        var audit = ParseAudit(plan.GenerationContextJson);

        return new DietDraftResponse
        {
            Id = plan.Id,
            Name = plan.Name,
            Status = plan.Status,
            Author = plan.Author,
            AuthorLabel = AuthorLabel(plan.Author, plan.ApprovedBy),
            Engine = audit.Engine,
            FallbackReason = audit.FallbackReason,
            MemberId = plan.MemberId,
            MemberName = plan.Member?.FullName,
            MemberCode = plan.Member?.MemberCode,
            TargetCalories = plan.TargetCalories,
            ProteinGrams = plan.ProteinGrams,
            CarbGrams = plan.CarbGrams,
            FatGrams = plan.FatGrams,
            IsVegetarian = plan.IsVegetarian,
            Notes = plan.Notes,
            ApprovedBy = plan.ApprovedBy,
            ApprovedAtUtc = plan.ApprovedAtUtc,
            CreatedAtUtc = plan.CreatedAtUtc,
            Meals = plan.Meals.OrderBy(m => m.OrderIndex).Select(m => new DietDraftMeal
            {
                Id = m.Id,
                Slot = m.Slot,
                SlotLabel = SlotLabel(m.Slot),
                Title = m.Title,
                Items = m.Items,
                Calories = m.Calories,
                ProteinGrams = m.ProteinGrams,
                CarbGrams = m.CarbGrams,
                FatGrams = m.FatGrams,
                TimingHint = m.TimingHint
            }).ToList()
        };
    }

    private static string AuthorLabel(PlanAuthor author, string? approvedBy) => author switch
    {
        PlanAuthor.Ai when approvedBy is not null => "AI draft, coach approved",
        PlanAuthor.Ai => "AI draft, awaiting approval",
        PlanAuthor.Admin when approvedBy is not null => "Rule-based draft, coach approved",
        PlanAuthor.Admin => "Rule-based draft, awaiting approval",
        _ => "Written by a coach"
    };

    internal static string SlotLabel(MealSlot slot) => slot switch
    {
        MealSlot.MidMorning => "Mid-morning",
        MealSlot.PreWorkout => "Pre-workout",
        MealSlot.PostWorkout => "Post-workout",
        _ => slot.ToString()
    };

    /// <summary>A malformed audit blob must never take the review screen down with it.</summary>
    private static (string? Engine, string? FallbackReason) ParseAudit(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return (null, null);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var engine = root.TryGetProperty("engine", out var e) ? e.GetString() : null;
            var reason = root.TryGetProperty("fallbackReason", out var r) && r.ValueKind != JsonValueKind.Null
                ? r.GetString()
                : null;
            return (engine, reason);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}

// ---------------------------------------------------------------- contracts

public record GeneratePlanRequest
{
    public string? Goal { get; init; }
    public ClassLevel? Level { get; init; }
    public int? DaysPerWeek { get; init; }
    public int? DurationWeeks { get; init; }
    public bool IsVegetarian { get; init; }
    public string? TrainerNote { get; init; }
    public int? TrainerId { get; init; }
}

public record UpdateWorkoutDraftRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Goal { get; init; }
    public int? DurationWeeks { get; init; }
    public IReadOnlyList<UpdateProgramDay>? Days { get; init; }
}

public record UpdateProgramDay
{
    public required int Id { get; init; }
    public string? Title { get; init; }
    public string? Focus { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<UpdateProgramExercise>? Exercises { get; init; }
}

public record UpdateProgramExercise
{
    public required int Id { get; init; }
    public int? Sets { get; init; }
    public string? RepScheme { get; init; }
    public int? RestSeconds { get; init; }
    public decimal? TargetWeightKg { get; init; }
    public string? Notes { get; init; }
    public bool Remove { get; init; }
}

public record UpdateDietDraftRequest
{
    public string? Name { get; init; }
    public string? Notes { get; init; }
    public int? TargetCalories { get; init; }
    public int? ProteinGrams { get; init; }
    public int? CarbGrams { get; init; }
    public int? FatGrams { get; init; }
    public IReadOnlyList<UpdateMeal>? Meals { get; init; }
}

public record UpdateMeal
{
    public required int Id { get; init; }
    public string? Title { get; init; }
    public string? Items { get; init; }
    public int? Calories { get; init; }
    public int? ProteinGrams { get; init; }
    public int? CarbGrams { get; init; }
    public int? FatGrams { get; init; }
    public string? TimingHint { get; init; }
    public bool Remove { get; init; }
}

public record WorkoutDraftResponse
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required ProgramStatus Status { get; init; }
    public required PlanAuthor Author { get; init; }
    public required string AuthorLabel { get; init; }
    public string? Engine { get; init; }
    public string? FallbackReason { get; init; }
    public string? Goal { get; init; }
    public int DaysPerWeek { get; init; }
    public int DurationWeeks { get; init; }
    public int? MemberId { get; init; }
    public string? MemberName { get; init; }
    public string? MemberCode { get; init; }
    public string? TrainerName { get; init; }
    public string? ApprovedBy { get; init; }
    public DateTime? ApprovedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public required IReadOnlyList<WorkoutDraftDay> Days { get; init; }
}

public record WorkoutDraftDay
{
    public required int Id { get; init; }
    public required int DayIndex { get; init; }
    public required string Title { get; init; }
    public string? Focus { get; init; }
    public bool IsRestDay { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<WorkoutDraftExercise> Exercises { get; init; }
}

public record WorkoutDraftExercise
{
    public required int Id { get; init; }
    public required int ExerciseId { get; init; }
    public required string Name { get; init; }
    public required string PrimaryMuscle { get; init; }
    public required string Equipment { get; init; }
    public required int Sets { get; init; }
    public required string RepScheme { get; init; }
    public required int RestSeconds { get; init; }
    public decimal? TargetWeightKg { get; init; }
    public string? SupersetGroup { get; init; }
    public string? Notes { get; init; }
}

public record DietDraftResponse
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required ProgramStatus Status { get; init; }
    public required PlanAuthor Author { get; init; }
    public required string AuthorLabel { get; init; }
    public string? Engine { get; init; }
    public string? FallbackReason { get; init; }
    public int? MemberId { get; init; }
    public string? MemberName { get; init; }
    public string? MemberCode { get; init; }
    public int TargetCalories { get; init; }
    public int ProteinGrams { get; init; }
    public int CarbGrams { get; init; }
    public int FatGrams { get; init; }
    public bool IsVegetarian { get; init; }
    public string? Notes { get; init; }
    public string? ApprovedBy { get; init; }
    public DateTime? ApprovedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public required IReadOnlyList<DietDraftMeal> Meals { get; init; }
}

public record DietDraftMeal
{
    public required int Id { get; init; }
    public required MealSlot Slot { get; init; }
    public required string SlotLabel { get; init; }
    public required string Title { get; init; }
    public required string Items { get; init; }
    public int Calories { get; init; }
    public int ProteinGrams { get; init; }
    public int CarbGrams { get; init; }
    public int FatGrams { get; init; }
    public string? TimingHint { get; init; }
}
