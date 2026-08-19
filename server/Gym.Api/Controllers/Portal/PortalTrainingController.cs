using Gym.Api.Contracts;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers.Portal;

/// <summary>
/// Workouts (Module 3 — Workouts): the assigned programme, the day the member is about to
/// train, set logging, and the PR engine behind the celebration banner.
///
/// Every exercise comes back with what the member did last time on it. That single number is
/// the reason a paper diary still beats most gym apps, and it is why the day payload is one
/// request rather than one per lift.
/// </summary>
[Route("api/portal")]
public class PortalTrainingController : PortalControllerBase
{
    private readonly GymDbContext _db;
    private readonly TrainingService _training;
    private readonly IClock _clock;

    public PortalTrainingController(GymDbContext db, TrainingService training, IClock clock)
    {
        _db = db;
        _training = training;
        _clock = clock;
    }

    /// <summary>The member's current programme, every day of it, with today's progress folded in.</summary>
    [HttpGet("program")]
    [ProducesResponseType(typeof(PortalProgramResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortalProgramResponse>> Program(CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var today = _clock.Today;
        var program = await CurrentProgramQuery(_db, memberId).FirstOrDefaultAsync(ct);
        if (program is null)
            return NotFound(new ProblemDetails
            {
                Title = "No programme assigned",
                Detail = "Your coach has not published a programme yet. You can still log any lift from the library.",
                Status = StatusCodes.Status404NotFound
            });

        var exerciseIds = program.Days
            .SelectMany(d => d.Exercises.Select(e => e.ExerciseId))
            .Distinct()
            .ToList();

        var logs = await _db.WorkoutLogs
            .AsNoTracking()
            .Where(l => l.MemberId == memberId && exerciseIds.Contains(l.ExerciseId))
            .OrderByDescending(l => l.PerformedOn).ThenBy(l => l.SetNumber)
            .Take(600)
            .ToListAsync(ct);

        var bests = await BestByExerciseAsync(_db, memberId, exerciseIds, ct);
        var byExercise = logs.ToLookup(l => l.ExerciseId);
        var trainer = program.Trainer;

        var days = program.Days
            .OrderBy(d => d.DayIndex)
            .Select(d => DescribeDay(d, byExercise, bests, today))
            .ToList();

        return Ok(new PortalProgramResponse
        {
            Id = program.Id,
            Name = program.Name,
            Description = program.Description,
            Goal = program.Goal,
            StatusName = program.Status.ToString(),
            AuthorName = program.Author switch
            {
                PlanAuthor.Ai => "AI draft, coach approved",
                PlanAuthor.Admin => "Written by the gym",
                _ => "Written by your coach"
            },
            DurationWeeks = program.DurationWeeks,
            DaysPerWeek = program.DaysPerWeek,
            WeekNumber = WeekNumber(program, today),
            StartsOn = program.StartsOn?.ToString("yyyy-MM-dd"),
            EndsOn = program.EndsOn?.ToString("yyyy-MM-dd"),
            TrainerName = trainer?.FullName,
            TrainerSlug = trainer?.Slug,
            TrainerPortraitUrl = trainer?.PortraitUrl ?? (trainer is null ? null : $"/media/trainers/{trainer.Slug}.jpg"),
            Days = days
        });
    }

    /// <summary>One training day, ready to log against.</summary>
    [HttpGet("program/days/{id:int}")]
    [ProducesResponseType(typeof(PortalProgramDayResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PortalProgramDayResponse>> Day(int id, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var day = await _db.ProgramDays
            .AsNoTracking()
            .Include(d => d.WorkoutProgram)
            .Include(d => d.Exercises).ThenInclude(e => e.Exercise)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        // A programme day belongs to exactly one member; a template day belongs to nobody.
        if (day is null || day.WorkoutProgram.MemberId != memberId) return NotFound();

        var exerciseIds = day.Exercises.Select(e => e.ExerciseId).Distinct().ToList();
        var logs = await _db.WorkoutLogs
            .AsNoTracking()
            .Where(l => l.MemberId == memberId && exerciseIds.Contains(l.ExerciseId))
            .OrderByDescending(l => l.PerformedOn).ThenBy(l => l.SetNumber)
            .Take(400)
            .ToListAsync(ct);

        var bests = await BestByExerciseAsync(_db, memberId, exerciseIds, ct);
        return Ok(DescribeDay(day, logs.ToLookup(l => l.ExerciseId), bests, _clock.Today));
    }

    /// <summary>The full exercise library, for logging a lift that is not on today's card.</summary>
    [HttpGet("exercises")]
    [ProducesResponseType(typeof(IReadOnlyList<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Exercises([FromQuery] string? q, CancellationToken ct)
    {
        var query = _db.Exercises.AsNoTracking().Where(e => e.IsActive);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(e => e.Name.Contains(term));
        }

        return Ok(await query
            .OrderBy(e => e.PrimaryMuscle).ThenBy(e => e.Name)
            .Take(200)
            .Select(e => new
            {
                e.Id,
                e.Name,
                e.Slug,
                PrimaryMuscle = e.PrimaryMuscle.ToString(),
                Equipment = e.Equipment.ToString(),
                e.IsStrengthTracked,
                e.Cues,
                e.VideoUrl,
                e.ThumbnailUrl
            })
            .ToListAsync(ct));
    }

    /// <summary>
    /// Logs one set. The response carries the PR verdict so the banner fires on the same round
    /// trip the set was saved on — a record the member has to refresh to discover is not a
    /// celebration.
    /// </summary>
    [HttpPost("workouts/sets")]
    [ProducesResponseType(typeof(PortalLogSetResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<PortalLogSetResponse>> LogSet(PortalLogSetRequest request, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        if (request.Reps is < 1 or > 500)
            return Problem("Reps have to be between 1 and 500.", statusCode: StatusCodes.Status400BadRequest);
        if (request.WeightKg is < 0 or > 1000)
            return Problem("That weight is not a number this gym has plates for.", statusCode: StatusCodes.Status400BadRequest);
        if (request.Rpe is { } rpe && rpe is < 1 or > 10)
            return Problem("RPE runs from 1 to 10.", statusCode: StatusCodes.Status400BadRequest);

        var performedOn = DateOnly.TryParse(request.PerformedOn, out var parsed) ? parsed : _clock.Today;
        if (performedOn > _clock.Today)
            return Problem("You cannot log a session that has not happened.", statusCode: StatusCodes.Status400BadRequest);

        // A programme exercise is only accepted if it is on this member's own programme.
        if (request.ProgramExerciseId is { } programExerciseId)
        {
            var owns = await _db.ProgramExercises
                .AnyAsync(pe => pe.Id == programExerciseId && pe.ProgramDay.WorkoutProgram.MemberId == memberId, ct);
            if (!owns) return NotFound();
        }

        SetOutcome outcome;
        try
        {
            outcome = await _training.LogSetAsync(
                memberId, request.ExerciseId, request.ProgramExerciseId, performedOn,
                Math.Clamp(request.SetNumber, 1, 50), request.Reps, request.WeightKg,
                request.Rpe, request.DurationSeconds, request.DistanceKm, request.Notes, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        var badges = await _db.MemberBadges
            .AsNoTracking()
            .Include(mb => mb.Badge)
            .Where(mb => mb.MemberId == memberId && !mb.IsSeen)
            .OrderByDescending(mb => mb.AwardedAtUtc)
            .Take(3)
            .ToListAsync(ct);

        return Created($"/api/portal/workouts/sets/{outcome.Log.Id}", new PortalLogSetResponse
        {
            Set = Describe(outcome.Log),
            IsPersonalRecord = outcome.IsPersonalRecord,
            PreviousBestE1Rm = outcome.PreviousBestE1Rm,
            Celebration = outcome.IsPersonalRecord ? Celebrate(outcome) : null,
            BadgesAwarded = badges.Select(mb => new PortalBadgeRow
            {
                Id = mb.BadgeId,
                Name = mb.Badge.Name,
                Slug = mb.Badge.Slug,
                Description = mb.Badge.Description,
                IconKey = mb.Badge.IconKey,
                Tier = mb.Badge.Tier,
                AwardedAtUtc = mb.AwardedAtUtc,
                IsSeen = mb.IsSeen
            }).ToList()
        });
    }

    /// <summary>Removes a mis-logged set. Records recompute from what is left, not from a cache.</summary>
    [HttpDelete("workouts/sets/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSet(int id, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var log = await _db.WorkoutLogs.FirstOrDefaultAsync(l => l.Id == id && l.MemberId == memberId, ct);
        if (log is null) return NotFound();

        // Only today's mistakes: history the member has already trained against stays put.
        if (log.PerformedOn < _clock.Today.AddDays(-1))
            return Problem("Sets older than yesterday are part of your history now.",
                statusCode: StatusCodes.Status400BadRequest);

        _db.WorkoutLogs.Remove(log);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Dismisses the PR banner so it fires exactly once per record.</summary>
    [HttpPost("workouts/celebrations/{logId:int}/seen")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkCelebrated(int logId, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var log = await _db.WorkoutLogs.FirstOrDefaultAsync(l => l.Id == logId && l.MemberId == memberId, ct);
        if (log is null) return NotFound();

        log.PrCelebrated = true;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Training history by day — what the member did, not what the programme said to do.</summary>
    [HttpGet("workouts/history")]
    [ProducesResponseType(typeof(IReadOnlyList<PortalWorkoutHistoryRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PortalWorkoutHistoryRow>>> History(
        [FromQuery] int days = 90, CancellationToken ct = default)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var from = _clock.Today.AddDays(-Math.Clamp(days, 7, 365));
        var logs = await _db.WorkoutLogs
            .AsNoTracking()
            .Where(l => l.MemberId == memberId && l.PerformedOn >= from)
            .Select(l => new { l.PerformedOn, l.Volume, l.IsPersonalRecord, ExerciseName = l.Exercise.Name })
            .ToListAsync(ct);

        return Ok(logs
            .GroupBy(l => l.PerformedOn)
            .OrderByDescending(g => g.Key)
            .Select(g => new PortalWorkoutHistoryRow
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                Sets = g.Count(),
                Volume = decimal.Round(g.Sum(l => l.Volume), 0),
                PersonalRecords = g.Count(l => l.IsPersonalRecord),
                Exercises = g.Select(l => l.ExerciseName).Distinct().OrderBy(n => n).ToList()
            })
            .ToList());
    }

    // ---------------------------------------------------------------- shared

    internal static IQueryable<WorkoutProgram> CurrentProgramQuery(GymDbContext db, int memberId) => db.WorkoutPrograms
        .AsNoTracking()
        .Include(p => p.Trainer)
        .Include(p => p.Days.OrderBy(d => d.DayIndex))
            .ThenInclude(d => d.Exercises.OrderBy(e => e.OrderIndex))
                .ThenInclude(e => e.Exercise)
        .Where(p => p.MemberId == memberId && !p.IsTemplate && p.Status == ProgramStatus.Published)
        .OrderByDescending(p => p.StartsOn ?? DateOnly.MinValue)
        .ThenByDescending(p => p.Id);

    /// <summary>The one-line programme card on the home screen, with the next day to train.</summary>
    internal static async Task<PortalProgramSummary?> LoadProgramSummaryAsync(
        GymDbContext db, int memberId, DateOnly today, CancellationToken ct)
    {
        var program = await CurrentProgramQuery(db, memberId).FirstOrDefaultAsync(ct);
        if (program is null) return null;

        var trainingDays = program.Days.Where(d => !d.IsRestDay).OrderBy(d => d.DayIndex).ToList();

        // "Next" is the day after the one most recently logged against — a programme is a
        // rotation, not a calendar, and a member who trains Tue/Thu should still get day 2.
        var programExerciseIds = program.Days.SelectMany(d => d.Exercises.Select(e => e.Id)).ToList();
        var lastLogged = await db.WorkoutLogs
            .AsNoTracking()
            .Where(l => l.MemberId == memberId && l.ProgramExerciseId != null
                     && programExerciseIds.Contains(l.ProgramExerciseId!.Value))
            .OrderByDescending(l => l.PerformedOn).ThenByDescending(l => l.Id)
            .Select(l => l.ProgramExerciseId)
            .FirstOrDefaultAsync(ct);

        ProgramDay? next = trainingDays.FirstOrDefault();
        if (lastLogged is { } lastId)
        {
            var lastDay = program.Days.FirstOrDefault(d => d.Exercises.Any(e => e.Id == lastId));
            if (lastDay is not null && trainingDays.Count > 0)
            {
                var position = trainingDays.FindIndex(d => d.Id == lastDay.Id);
                next = trainingDays[(position + 1) % trainingDays.Count];
            }
        }

        var sessionsLogged = await db.WorkoutLogs
            .AsNoTracking()
            .Where(l => l.MemberId == memberId && l.ProgramExerciseId != null
                     && programExerciseIds.Contains(l.ProgramExerciseId!.Value))
            .Select(l => l.PerformedOn)
            .Distinct()
            .CountAsync(ct);

        return new PortalProgramSummary
        {
            Id = program.Id,
            Name = program.Name,
            Goal = program.Goal,
            WeekNumber = WeekNumber(program, today),
            DurationWeeks = program.DurationWeeks,
            DaysPerWeek = program.DaysPerWeek,
            TrainerName = program.Trainer?.FullName,
            NextDayId = next?.Id,
            NextDayTitle = next?.Title,
            SessionsLogged = sessionsLogged
        };
    }

    /// <summary>The most recent record the member has not been shown yet, if any.</summary>
    internal static async Task<PortalPrCelebration?> LoadPendingCelebrationAsync(
        GymDbContext db, int memberId, CancellationToken ct)
    {
        var log = await db.WorkoutLogs
            .AsNoTracking()
            .Include(l => l.Exercise)
            .Where(l => l.MemberId == memberId && l.IsPersonalRecord && !l.PrCelebrated)
            .OrderByDescending(l => l.PerformedAtUtc)
            .FirstOrDefaultAsync(ct);
        if (log is null) return null;

        var previous = await db.WorkoutLogs
            .AsNoTracking()
            .Where(l => l.MemberId == memberId && l.ExerciseId == log.ExerciseId && l.Id != log.Id)
            .MaxAsync(l => (decimal?)l.EstimatedOneRepMax, ct);

        return Celebrate(new SetOutcome(log, true, previous, log.Exercise.Name));
    }

    private static PortalPrCelebration Celebrate(SetOutcome outcome)
    {
        var log = outcome.Log;
        var gain = outcome.PreviousBestE1Rm is > 0
            ? decimal.Round(log.EstimatedOneRepMax - outcome.PreviousBestE1Rm.Value, 1)
            : (decimal?)null;

        return new PortalPrCelebration
        {
            LogId = log.Id,
            ExerciseName = outcome.ExerciseName,
            WeightKg = log.WeightKg,
            Reps = log.Reps,
            EstimatedOneRepMax = log.EstimatedOneRepMax,
            PreviousBestE1Rm = outcome.PreviousBestE1Rm,
            Headline = "Personal record",
            Message = gain is null
                ? $"{log.WeightKg:0.#} kg × {log.Reps} on the {outcome.ExerciseName.ToLowerInvariant()} — your first record on this lift."
                : $"{log.WeightKg:0.#} kg × {log.Reps} — {gain:0.#} kg better than your last best.",
            ShareText = $"New PR: {outcome.ExerciseName} {log.WeightKg:0.#} kg × {log.Reps}. Estimated 1RM {log.EstimatedOneRepMax:0.#} kg.",
            PerformedOn = log.PerformedOn.ToString("yyyy-MM-dd")
        };
    }

    private static int WeekNumber(WorkoutProgram program, DateOnly today)
    {
        if (program.StartsOn is not { } start) return 1;
        var elapsed = today.DayNumber - start.DayNumber;
        return Math.Clamp(elapsed / 7 + 1, 1, Math.Max(1, program.DurationWeeks));
    }

    /// <summary>Best estimated 1RM per lift over the member's whole history, not a page of it.</summary>
    private static async Task<Dictionary<int, decimal>> BestByExerciseAsync(
        GymDbContext db, int memberId, List<int> exerciseIds, CancellationToken ct) =>
        await db.WorkoutLogs
            .AsNoTracking()
            .Where(l => l.MemberId == memberId && exerciseIds.Contains(l.ExerciseId))
            .GroupBy(l => l.ExerciseId)
            .Select(g => new { ExerciseId = g.Key, Best = g.Max(l => l.EstimatedOneRepMax) })
            .ToDictionaryAsync(x => x.ExerciseId, x => x.Best, ct);

    private static PortalProgramDayResponse DescribeDay(
        ProgramDay day, ILookup<int, WorkoutLog> byExercise, IReadOnlyDictionary<int, decimal> bests, DateOnly today)
    {
        var exercises = day.Exercises.OrderBy(e => e.OrderIndex).Select(pe =>
        {
            var history = byExercise[pe.ExerciseId].ToList();
            var todaySets = history.Where(l => l.PerformedOn == today).OrderBy(l => l.SetNumber).ToList();
            var lastDate = history.Where(l => l.PerformedOn < today).Select(l => l.PerformedOn).DefaultIfEmpty().Max();
            var lastSets = lastDate == default
                ? new List<WorkoutLog>()
                : history.Where(l => l.PerformedOn == lastDate).OrderBy(l => l.SetNumber).ToList();

            return new PortalProgramExerciseResponse
            {
                Id = pe.Id,
                ExerciseId = pe.ExerciseId,
                Name = pe.Exercise.Name,
                Slug = pe.Exercise.Slug,
                PrimaryMuscle = pe.Exercise.PrimaryMuscle.ToString(),
                Equipment = pe.Exercise.Equipment.ToString(),
                VideoUrl = pe.Exercise.VideoUrl,
                ThumbnailUrl = pe.Exercise.ThumbnailUrl,
                Cues = pe.Exercise.Cues,
                IsStrengthTracked = pe.Exercise.IsStrengthTracked,
                OrderIndex = pe.OrderIndex,
                Sets = pe.Sets,
                RepScheme = pe.RepScheme,
                RestSeconds = pe.RestSeconds,
                TargetWeightKg = pe.TargetWeightKg,
                Tempo = pe.Tempo,
                SupersetGroup = pe.SupersetGroup,
                Notes = pe.Notes,
                LastSession = lastSets.Select(Describe).ToList(),
                LastSessionOn = lastDate == default ? null : lastDate.ToString("yyyy-MM-dd"),
                BestE1Rm = bests.TryGetValue(pe.ExerciseId, out var best) ? best : null,
                TodaySets = todaySets.Select(Describe).ToList()
            };
        }).ToList();

        var totalSets = exercises.Sum(e => e.Sets);
        var lastPerformed = day.Exercises
            .SelectMany(pe => byExercise[pe.ExerciseId])
            .Where(l => l.ProgramExerciseId != null)
            .Select(l => l.PerformedOn)
            .DefaultIfEmpty()
            .Max();

        return new PortalProgramDayResponse
        {
            Id = day.Id,
            DayIndex = day.DayIndex,
            Title = day.Title,
            Focus = day.Focus,
            IsRestDay = day.IsRestDay,
            Notes = day.Notes,
            ExerciseCount = exercises.Count,
            TotalSets = totalSets,
            // Working set plus its rest, rounded up to the nearest five so it reads as an estimate.
            EstimatedMinutes = (int)(Math.Ceiling(
                exercises.Sum(e => e.Sets * (e.RestSeconds + 45)) / 300d) * 5),
            LastPerformedOn = lastPerformed == default ? null : lastPerformed.ToString("yyyy-MM-dd"),
            Exercises = exercises
        };
    }

    private static PortalSetRow Describe(WorkoutLog l) => new()
    {
        Id = l.Id,
        SetNumber = l.SetNumber,
        Reps = l.Reps,
        WeightKg = l.WeightKg,
        Rpe = l.Rpe,
        Volume = l.Volume,
        EstimatedOneRepMax = l.EstimatedOneRepMax,
        IsPersonalRecord = l.IsPersonalRecord,
        PerformedOn = l.PerformedOn.ToString("yyyy-MM-dd"),
        Notes = l.Notes
    };
}
