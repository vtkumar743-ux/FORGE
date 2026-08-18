using Gym.Api.Contracts;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers;

/// <summary>
/// The public class format library and timetable (Module 1.3). Anonymous — the spec is
/// explicit that no login is required to browse or filter, only to book.
/// </summary>
[ApiController]
[Route("api/classes")]
[Produces("application/json")]
[AllowAnonymous]
public class ClassesController : ControllerBase
{
    private readonly GymDbContext _db;
    private readonly IClock _clock;

    public ClassesController(GymDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>
    /// Every format shown on the website, each with its live session count, the branches
    /// that run it and the next bookable slot. The class rail renders straight from this.
    /// </summary>
    [HttpGet("formats")]
    [ProducesResponseType(typeof(IReadOnlyList<ClassFormatResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassFormatResponse>>> Formats(
        [FromQuery] string? branchSlug, CancellationToken ct)
    {
        var today = _clock.Today;
        var horizon = today.AddDays(7);
        var nowUtc = _clock.UtcNow;

        var formats = await _db.ClassFormats
            .AsNoTracking()
            .Where(f => f.IsActive && f.ShowOnWebsite)
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync(ct);

        var upcoming = await SessionQuery(branchSlug, null, null, null, null)
            .Where(s => s.SessionDate >= today && s.SessionDate <= horizon && s.Status == SessionStatus.Scheduled)
            .Select(s => new
            {
                s.ClassFormatId,
                s.BranchId,
                BranchSlug = s.Branch.Slug,
                s.StartsAtUtc,
                SessionId = s.Id
            })
            .ToListAsync(ct);

        var nextIds = upcoming
            .Where(s => s.StartsAtUtc > nowUtc)
            .GroupBy(s => s.ClassFormatId)
            .Select(g => g.OrderBy(s => s.StartsAtUtc).First().SessionId)
            .ToList();

        var nextSessions = (await LoadSessionsAsync(s => nextIds.Contains(s.Id), ct))
            .ToDictionary(s => s.FormatSlug);

        var byFormat = upcoming.ToLookup(s => s.ClassFormatId);

        return Ok(formats.Select(f =>
        {
            var sessions = byFormat[f.Id].ToList();
            return new ClassFormatResponse
            {
                Id = f.Id,
                Name = f.Name,
                Slug = f.Slug,
                ShortDescription = f.ShortDescription,
                Description = f.Description,
                DurationMinutes = f.DefaultDurationMinutes,
                Capacity = f.DefaultCapacity,
                Level = f.Level,
                LevelName = LevelLabel(f.Level),
                Intensity = f.Intensity,
                IntensityName = f.Intensity.ToString(),
                EstimatedCalories = f.EstimatedCalories,
                CoverImageUrl = f.CoverImageUrl ?? $"/media/classes/{f.Slug}.jpg",
                IconKey = f.IconKey,
                Tags = Split(f.Tags),
                DisplayOrder = f.DisplayOrder,
                UpcomingSessionCount = sessions.Count,
                BranchSlugs = sessions.Select(s => s.BranchSlug).Distinct().OrderBy(s => s).ToList(),
                NextSession = nextSessions.GetValueOrDefault(f.Slug)
            };
        }).ToList());
    }

    /// <summary>
    /// The filterable public timetable. Returns the sessions plus the format and trainer
    /// facets for the current filter set, so the filter pills only ever offer live options.
    /// </summary>
    [HttpGet("timetable")]
    [ProducesResponseType(typeof(TimetableResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TimetableResponse>> Timetable(
        [FromQuery] string? branchSlug,
        [FromQuery] string? formatSlug,
        [FromQuery] string? trainerSlug,
        [FromQuery] string? level,
        [FromQuery] string? timeOfDay,
        [FromQuery] DateOnly? from,
        [FromQuery] int days = 7,
        CancellationToken ct = default)
    {
        var today = _clock.Today;
        // Never expose a past timetable: bookings can only be made forwards.
        var start = from is { } requested && requested > today ? requested : today;
        var span = Math.Clamp(days, 1, 21);
        var end = start.AddDays(span - 1);

        var parsedLevel = Enum.TryParse<ClassLevel>(level, ignoreCase: true, out var lv) ? lv : (ClassLevel?)null;

        var sessions = await LoadSessionsAsync(
            s => s.SessionDate >= start && s.SessionDate <= end &&
                 (branchSlug == null || s.Branch.Slug == branchSlug) &&
                 (formatSlug == null || s.ClassFormat.Slug == formatSlug) &&
                 (trainerSlug == null || s.Trainer.Slug == trainerSlug ||
                  (s.SubstituteTrainer != null && s.SubstituteTrainer.Slug == trainerSlug)) &&
                 (parsedLevel == null || s.ClassFormat.Level == parsedLevel),
            ct);

        // Time-of-day is a derived bucket, so it filters in memory after projection.
        if (!string.IsNullOrWhiteSpace(timeOfDay))
            sessions = sessions
                .Where(s => string.Equals(s.TimeOfDay, timeOfDay, StringComparison.OrdinalIgnoreCase))
                .ToList();

        return Ok(new TimetableResponse
        {
            FromDate = start.ToString("yyyy-MM-dd"),
            ToDate = end.ToString("yyyy-MM-dd"),
            Sessions = sessions,
            Formats = sessions
                .GroupBy(s => (s.FormatSlug, s.FormatName))
                .Select(g => new TimetableFilterOption { Slug = g.Key.FormatSlug, Name = g.Key.FormatName, Count = g.Count() })
                .OrderBy(o => o.Name)
                .ToList(),
            Trainers = sessions
                .GroupBy(s => (s.TrainerSlug, s.TrainerName))
                .Select(g => new TimetableFilterOption { Slug = g.Key.TrainerSlug, Name = g.Key.TrainerName, Count = g.Count() })
                .OrderBy(o => o.Name)
                .ToList()
        });
    }

    // ---------------------------------------------------------------- helpers

    private IQueryable<ClassSession> SessionQuery(
        string? branchSlug, string? formatSlug, string? trainerSlug, DateOnly? from, DateOnly? to)
    {
        var query = _db.ClassSessions.AsNoTracking().Where(s => s.Status != SessionStatus.Cancelled);
        if (branchSlug is not null) query = query.Where(s => s.Branch.Slug == branchSlug);
        if (formatSlug is not null) query = query.Where(s => s.ClassFormat.Slug == formatSlug);
        if (trainerSlug is not null) query = query.Where(s => s.Trainer.Slug == trainerSlug);
        if (from is not null) query = query.Where(s => s.SessionDate >= from);
        if (to is not null) query = query.Where(s => s.SessionDate <= to);
        return query;
    }

    /// <summary>One projection shared by the rail and the timetable so both read identically.</summary>
    private async Task<List<ClassSessionResponse>> LoadSessionsAsync(
        System.Linq.Expressions.Expression<Func<ClassSession, bool>> predicate, CancellationToken ct)
    {
        var nowUtc = _clock.UtcNow;

        var rows = await _db.ClassSessions
            .AsNoTracking()
            .Where(s => s.Status != SessionStatus.Cancelled)
            .Where(predicate)
            .OrderBy(s => s.StartsAtUtc)
            .Select(s => new
            {
                s.Id,
                FormatName = s.ClassFormat.Name,
                FormatSlug = s.ClassFormat.Slug,
                s.ClassFormat.IconKey,
                s.ClassFormat.CoverImageUrl,
                s.ClassFormat.Level,
                s.ClassFormat.Intensity,
                s.BranchId,
                BranchName = s.Branch.Name,
                BranchSlug = s.Branch.Slug,
                TrainerName = s.SubstituteTrainer != null ? s.SubstituteTrainer.FullName : s.Trainer.FullName,
                TrainerSlug = s.SubstituteTrainer != null ? s.SubstituteTrainer.Slug : s.Trainer.Slug,
                TrainerPortraitUrl = s.SubstituteTrainer != null ? s.SubstituteTrainer.PortraitUrl : s.Trainer.PortraitUrl,
                IsSubstitute = s.SubstituteTrainerId != null,
                RoomName = s.Room != null ? s.Room.Name : null,
                s.SessionDate,
                s.StartTime,
                s.StartsAtUtc,
                s.DurationMinutes,
                s.Capacity,
                s.BookedCount,
                s.WaitlistCount,
                s.Status
            })
            .ToListAsync(ct);

        return rows.Select(s => new ClassSessionResponse
        {
            Id = s.Id,
            FormatName = s.FormatName,
            FormatSlug = s.FormatSlug,
            IconKey = s.IconKey,
            CoverImageUrl = s.CoverImageUrl ?? $"/media/classes/{s.FormatSlug}.jpg",
            Level = s.Level,
            LevelName = LevelLabel(s.Level),
            Intensity = s.Intensity,
            BranchId = s.BranchId,
            BranchName = s.BranchName,
            BranchSlug = s.BranchSlug,
            TrainerName = s.TrainerName,
            TrainerSlug = s.TrainerSlug,
            TrainerPortraitUrl = s.TrainerPortraitUrl ?? $"/media/trainers/{s.TrainerSlug}.jpg",
            IsSubstitute = s.IsSubstitute,
            RoomName = s.RoomName,
            Date = s.SessionDate.ToString("yyyy-MM-dd"),
            StartTime = s.StartTime.ToString("HH\\:mm"),
            EndTime = s.StartTime.AddMinutes(s.DurationMinutes).ToString("HH\\:mm"),
            StartsAtUtc = s.StartsAtUtc,
            DurationMinutes = s.DurationMinutes,
            Capacity = s.Capacity,
            BookedCount = s.BookedCount,
            SpotsLeft = Math.Max(0, s.Capacity - s.BookedCount),
            WaitlistCount = s.WaitlistCount,
            Status = s.Status,
            IsBookable = s.Status == SessionStatus.Scheduled && s.StartsAtUtc > nowUtc,
            TimeOfDay = Bucket(s.StartTime)
        }).ToList();
    }

    /// <summary>Buckets match the free-trial form's options so one vocabulary runs site-wide.</summary>
    private static string Bucket(TimeOnly start) => start.Hour switch
    {
        < 8 => "Early morning",
        < 11 => "Morning",
        < 16 => "Midday",
        < 19 => "Evening",
        _ => "Late evening"
    };

    private static string LevelLabel(ClassLevel level) => level switch
    {
        ClassLevel.AllLevels => "All levels",
        _ => level.ToString()
    };

    internal static IReadOnlyList<string> Split(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? Array.Empty<string>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
