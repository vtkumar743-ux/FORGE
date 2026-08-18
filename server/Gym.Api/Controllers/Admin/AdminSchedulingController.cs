using Gym.Api.Contracts;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers.Admin;

/// <summary>
/// Classes and scheduling: the shared format library, per-branch rooms, the recurring
/// timetable builder with conflict detection, and the day-to-day session work — rosters,
/// substitutions, cancellations, desk bookings and waitlist promotion.
/// </summary>
[ApiController]
[Route("api/admin/scheduling")]
[Authorize(Roles = RoleNames.Admin)]
[Produces("application/json")]
public class AdminSchedulingController : ControllerBase
{
    private readonly GymDbContext _db;
    private readonly SchedulingService _scheduling;
    private readonly INotificationDispatcher _notifier;
    private readonly IClock _clock;
    private readonly ILogger<AdminSchedulingController> _log;

    public AdminSchedulingController(
        GymDbContext db, SchedulingService scheduling, INotificationDispatcher notifier,
        IClock clock, ILogger<AdminSchedulingController> log)
    {
        _db = db;
        _scheduling = scheduling;
        _notifier = notifier;
        _clock = clock;
        _log = log;
    }

    // ================================================================== formats

    [HttpGet("formats")]
    [ProducesResponseType(typeof(IReadOnlyList<ClassFormatRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassFormatRow>>> Formats(CancellationToken ct)
    {
        var formats = await _db.ClassFormats.AsNoTracking()
            .OrderBy(f => f.DisplayOrder).ThenBy(f => f.Name)
            .Select(f => new ClassFormatRow
            {
                Id = f.Id, Name = f.Name, Slug = f.Slug, ShortDescription = f.ShortDescription,
                Description = f.Description, DefaultDurationMinutes = f.DefaultDurationMinutes,
                DefaultCapacity = f.DefaultCapacity, Level = f.Level, Intensity = f.Intensity,
                EstimatedCalories = f.EstimatedCalories, CoverImageUrl = f.CoverImageUrl,
                IconKey = f.IconKey, Tags = f.Tags, ShowOnWebsite = f.ShowOnWebsite,
                IsActive = f.IsActive, DisplayOrder = f.DisplayOrder,
                WeeklySlots = f.Schedules.Count(s => s.IsActive)
            })
            .ToListAsync(ct);

        return Ok(formats);
    }

    [HttpPost("formats")]
    public async Task<IActionResult> CreateFormat(UpsertClassFormatRequest request, CancellationToken ct)
    {
        var slug = AdminCmsController.Slugify(request.Slug);
        if (await _db.ClassFormats.AnyAsync(f => f.Slug == slug, ct))
        {
            ModelState.AddModelError(nameof(request.Slug), "A class format already uses that slug.");
            return ValidationProblem(ModelState);
        }

        var format = new ClassFormat { Slug = slug, CreatedBy = User.Identity?.Name };
        Apply(format, request);
        _db.ClassFormats.Add(format);
        await _db.SaveChangesAsync(ct);
        return Created($"/api/admin/scheduling/formats/{format.Id}", new { format.Id, format.Slug });
    }

    [HttpPut("formats/{id:int}")]
    public async Task<IActionResult> UpdateFormat(int id, UpsertClassFormatRequest request, CancellationToken ct)
    {
        var format = await _db.ClassFormats.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (format is null) return NotFound();

        var slug = AdminCmsController.Slugify(request.Slug);
        if (slug != format.Slug && await _db.ClassFormats.AnyAsync(f => f.Slug == slug && f.Id != id, ct))
        {
            ModelState.AddModelError(nameof(request.Slug), "A class format already uses that slug.");
            return ValidationProblem(ModelState);
        }

        format.Slug = slug;
        Apply(format, request);
        format.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("formats/{id:int}")]
    public async Task<IActionResult> DeleteFormat(int id, CancellationToken ct)
    {
        var format = await _db.ClassFormats.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (format is null) return NotFound();

        if (await _db.ClassSchedules.AnyAsync(s => s.ClassFormatId == id, ct))
        {
            format.IsActive = false;
            format.ShowOnWebsite = false;
            await _db.SaveChangesAsync(ct);
            return Ok(new { retired = true, message = "The format is on the timetable, so it was retired rather than deleted." });
        }

        _db.ClassFormats.Remove(format);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Every coach who can be rostered — including those hidden from the website, which the
    /// public /api/trainers list deliberately excludes.
    /// </summary>
    [HttpGet("trainers")]
    public async Task<IActionResult> Trainers([FromQuery] int? branchId, CancellationToken ct) =>
        Ok(await _db.Trainers.AsNoTracking()
            .Where(t => t.IsActive && (branchId == null || t.PrimaryBranchId == branchId))
            .OrderBy(t => t.FullName)
            .Select(t => new
            {
                t.Id, t.FullName, t.Slug, t.PrimaryBranchId,
                BranchName = t.PrimaryBranch.Name, t.PortraitUrl, t.PerClassRate, t.ShowOnWebsite
            })
            .ToListAsync(ct));

    // ================================================================== rooms

    [HttpGet("rooms")]
    [ProducesResponseType(typeof(IReadOnlyList<RoomRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoomRow>>> Rooms([FromQuery] int? branchId, CancellationToken ct) =>
        Ok(await _db.Rooms.AsNoTracking()
            .Where(r => branchId == null || r.BranchId == branchId)
            .OrderBy(r => r.Branch.DisplayOrder).ThenBy(r => r.Name)
            .Select(r => new RoomRow
            {
                Id = r.Id, BranchId = r.BranchId, BranchName = r.Branch.Name, Name = r.Name,
                Capacity = r.Capacity, Notes = r.Notes, IsActive = r.IsActive
            })
            .ToListAsync(ct));

    [HttpPost("rooms")]
    public async Task<IActionResult> CreateRoom(UpsertRoomRequest request, CancellationToken ct)
    {
        if (await _db.Rooms.AnyAsync(r => r.BranchId == request.BranchId && r.Name == request.Name, ct))
        {
            ModelState.AddModelError(nameof(request.Name), "That branch already has a room with this name.");
            return ValidationProblem(ModelState);
        }

        var room = new Room
        {
            BranchId = request.BranchId, Name = request.Name.Trim(), Capacity = request.Capacity,
            Notes = request.Notes, IsActive = request.IsActive, CreatedBy = User.Identity?.Name
        };
        _db.Rooms.Add(room);
        await _db.SaveChangesAsync(ct);
        return Created($"/api/admin/scheduling/rooms/{room.Id}", new { room.Id });
    }

    [HttpPut("rooms/{id:int}")]
    public async Task<IActionResult> UpdateRoom(int id, UpsertRoomRequest request, CancellationToken ct)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (room is null) return NotFound();

        room.BranchId = request.BranchId;
        room.Name = request.Name.Trim();
        room.Capacity = request.Capacity;
        room.Notes = request.Notes;
        room.IsActive = request.IsActive;
        room.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ================================================================== recurring schedules

    [HttpGet("schedules")]
    [ProducesResponseType(typeof(IReadOnlyList<ScheduleRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ScheduleRow>>> Schedules(
        [FromQuery] int? branchId, [FromQuery] int? trainerId, [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var today = _clock.Today;

        var rows = await _db.ClassSchedules.AsNoTracking()
            .Where(s => (branchId == null || s.BranchId == branchId)
                     && (trainerId == null || s.TrainerId == trainerId)
                     && (includeInactive || s.IsActive))
            .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
            .Select(s => new
            {
                s.Id, s.BranchId, BranchName = s.Branch.Name, s.ClassFormatId,
                FormatName = s.ClassFormat.Name, s.ClassFormat.IconKey, s.RoomId,
                RoomName = s.Room != null ? s.Room.Name : null, s.TrainerId,
                TrainerName = s.Trainer.FullName, s.DayOfWeek, s.StartTime, s.DurationMinutes,
                s.Capacity, s.EffectiveFrom, s.EffectiveTo, s.BookingOpensHoursBefore,
                s.CancelCutoffHoursBefore, s.WaitlistEnabled, s.WaitlistCapacity, s.IsActive,
                Upcoming = s.Sessions.Count(x => x.SessionDate >= today && x.Status != SessionStatus.Cancelled),
                BookedSum = s.Sessions.Where(x => x.SessionDate >= today.AddDays(-28) && x.SessionDate < today)
                    .Sum(x => (int?)x.BookedCount) ?? 0,
                CapacitySum = s.Sessions.Where(x => x.SessionDate >= today.AddDays(-28) && x.SessionDate < today)
                    .Sum(x => (int?)x.Capacity) ?? 0
            })
            .ToListAsync(ct);

        return Ok(rows.Select(s => new ScheduleRow
        {
            Id = s.Id, BranchId = s.BranchId, BranchName = s.BranchName, ClassFormatId = s.ClassFormatId,
            FormatName = s.FormatName, IconKey = s.IconKey, RoomId = s.RoomId, RoomName = s.RoomName,
            TrainerId = s.TrainerId, TrainerName = s.TrainerName, DayOfWeek = s.DayOfWeek,
            StartTime = s.StartTime.ToString("HH\\:mm"),
            EndTime = s.StartTime.AddMinutes(s.DurationMinutes).ToString("HH\\:mm"),
            DurationMinutes = s.DurationMinutes, Capacity = s.Capacity,
            EffectiveFrom = s.EffectiveFrom.ToString("yyyy-MM-dd"),
            EffectiveTo = s.EffectiveTo?.ToString("yyyy-MM-dd"),
            BookingOpensHoursBefore = s.BookingOpensHoursBefore,
            CancelCutoffHoursBefore = s.CancelCutoffHoursBefore,
            WaitlistEnabled = s.WaitlistEnabled, WaitlistCapacity = s.WaitlistCapacity,
            IsActive = s.IsActive, UpcomingSessions = s.Upcoming,
            // Fill rate over the last four weeks — the number that says whether a slot works.
            AverageFillPercent = s.CapacitySum == 0 ? 0 : (int)Math.Round(s.BookedSum * 100d / s.CapacitySum)
        }).ToList());
    }

    [HttpPost("schedules/check-conflicts")]
    [ProducesResponseType(typeof(IReadOnlyList<ConflictRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConflictRow>>> CheckConflicts(
        ConflictCheckRequest request, CancellationToken ct)
    {
        var conflicts = await _scheduling.FindConflictsAsync(
            request.BranchId, request.TrainerId, request.RoomId, request.DayOfWeek, request.StartTime,
            request.DurationMinutes, request.EffectiveFrom, request.EffectiveTo, request.IgnoreScheduleId, ct);

        return Ok(conflicts.Select(c => new ConflictRow
        {
            Kind = c.Kind, Message = c.Message,
            ConflictingScheduleId = c.ConflictingScheduleId, ConflictingLabel = c.ConflictingLabel
        }).ToList());
    }

    [HttpPost("schedules")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSchedule(UpsertScheduleRequest request, CancellationToken ct)
    {
        var conflicts = await _scheduling.FindConflictsAsync(
            request.BranchId, request.TrainerId, request.RoomId, request.DayOfWeek, request.StartTime,
            request.DurationMinutes, request.EffectiveFrom, request.EffectiveTo, null, ct);

        if (conflicts.Count > 0 && !request.IgnoreConflicts)
            return Conflict(new { conflicts = conflicts.Select(Describe).ToList() });

        var schedule = new ClassSchedule { CreatedBy = User.Identity?.Name };
        Apply(schedule, request);
        _db.ClassSchedules.Add(schedule);
        await _db.SaveChangesAsync(ct);

        var created = request.MaterialiseWeeks > 0
            ? await _scheduling.MaterialiseAsync(
                schedule, _clock.Today, _clock.Today.AddDays(request.MaterialiseWeeks * 7), ct)
            : 0;

        _log.LogInformation("Timetable slot {Format} {Day} {Time} created with {Sessions} occurrence(s)",
            schedule.ClassFormatId, schedule.DayOfWeek, schedule.StartTime, created);

        return Created($"/api/admin/scheduling/schedules/{schedule.Id}",
            new { schedule.Id, sessionsCreated = created, conflicts = conflicts.Select(Describe).ToList() });
    }

    [HttpPut("schedules/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateSchedule(int id, UpsertScheduleRequest request, CancellationToken ct)
    {
        var schedule = await _db.ClassSchedules.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (schedule is null) return NotFound();

        var conflicts = await _scheduling.FindConflictsAsync(
            request.BranchId, request.TrainerId, request.RoomId, request.DayOfWeek, request.StartTime,
            request.DurationMinutes, request.EffectiveFrom, request.EffectiveTo, id, ct);

        if (conflicts.Count > 0 && !request.IgnoreConflicts)
            return Conflict(new { conflicts = conflicts.Select(Describe).ToList() });

        var timeChanged = schedule.StartTime != request.StartTime
                       || schedule.DayOfWeek != request.DayOfWeek
                       || schedule.DurationMinutes != request.DurationMinutes;

        Apply(schedule, request);
        schedule.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync(ct);

        // Future occurrences carry the rule's details; already-booked ones move with it rather
        // than being deleted, because members hold a booking against that exact session row.
        var today = _clock.Today;
        var future = await _db.ClassSessions
            .Where(s => s.ClassScheduleId == id && s.SessionDate >= today && s.Status == SessionStatus.Scheduled)
            .ToListAsync(ct);

        foreach (var session in future)
        {
            session.TrainerId = schedule.TrainerId;
            session.RoomId = schedule.RoomId;
            session.ClassFormatId = schedule.ClassFormatId;
            session.Capacity = schedule.Capacity;
            if (timeChanged)
            {
                var startsIst = session.SessionDate.ToDateTime(schedule.StartTime);
                session.StartTime = schedule.StartTime;
                session.DurationMinutes = schedule.DurationMinutes;
                session.StartsAtUtc = startsIst - SchedulingService.IstOffset;
                session.EndsAtUtc = startsIst.AddMinutes(schedule.DurationMinutes) - SchedulingService.IstOffset;
            }
        }

        // A moved slot leaves occurrences on the wrong weekday; drop the unbooked ones and rebuild.
        if (timeChanged)
        {
            var orphans = future.Where(s => s.SessionDate.DayOfWeek != schedule.DayOfWeek && s.BookedCount == 0).ToList();
            _db.ClassSessions.RemoveRange(orphans);
        }

        await _db.SaveChangesAsync(ct);

        var created = request.MaterialiseWeeks > 0
            ? await _scheduling.MaterialiseAsync(schedule, today, today.AddDays(request.MaterialiseWeeks * 7), ct)
            : 0;

        return Ok(new { updatedSessions = future.Count, sessionsCreated = created });
    }

    [HttpDelete("schedules/{id:int}")]
    public async Task<IActionResult> DeleteSchedule(int id, CancellationToken ct)
    {
        var schedule = await _db.ClassSchedules.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (schedule is null) return NotFound();

        var today = _clock.Today;
        var booked = await _db.ClassSessions
            .AnyAsync(s => s.ClassScheduleId == id && s.SessionDate >= today && s.BookedCount > 0, ct);

        // Ending the rule today keeps history and honours bookings already taken.
        schedule.IsActive = false;
        schedule.EffectiveTo = today;

        var unbooked = await _db.ClassSessions
            .Where(s => s.ClassScheduleId == id && s.SessionDate > today && s.BookedCount == 0)
            .ToListAsync(ct);
        _db.ClassSessions.RemoveRange(unbooked);

        await _db.SaveChangesAsync(ct);
        return Ok(new { retired = true, removedSessions = unbooked.Count, hasBookedSessions = booked });
    }

    [HttpPost("schedules/{id:int}/materialise")]
    public async Task<IActionResult> Materialise(int id, [FromQuery] int weeks = 4, CancellationToken ct = default)
    {
        var schedule = await _db.ClassSchedules.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (schedule is null) return NotFound();

        var span = Math.Clamp(weeks, 1, 26);
        var created = await _scheduling.MaterialiseAsync(schedule, _clock.Today, _clock.Today.AddDays(span * 7), ct);
        return Ok(new { sessionsCreated = created });
    }

    // ================================================================== sessions

    [HttpGet("sessions")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminSessionRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminSessionRow>>> Sessions(
        [FromQuery] int? branchId, [FromQuery] DateOnly? from, [FromQuery] int? trainerId,
        [FromQuery] int? formatId, [FromQuery] int days = 7, CancellationToken ct = default)
    {
        var start = from ?? _clock.Today;
        var end = start.AddDays(Math.Clamp(days, 1, 42) - 1);

        // Loaded with navigations rather than projected in SQL: the row carries formatted IST
        // clock strings, which is a client-side concern, not something to push into the query.
        var sessions = await SessionQuery()
            .Where(s => s.SessionDate >= start && s.SessionDate <= end)
            .Where(s => branchId == null || s.BranchId == branchId)
            .Where(s => trainerId == null || s.TrainerId == trainerId || s.SubstituteTrainerId == trainerId)
            .Where(s => formatId == null || s.ClassFormatId == formatId)
            .OrderBy(s => s.SessionDate).ThenBy(s => s.StartTime)
            .ToListAsync(ct);

        return Ok(sessions.Select(Project).ToList());
    }

    [HttpGet("sessions/{id:int}/roster")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Roster(int id, CancellationToken ct)
    {
        var entity = await SessionQuery().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (entity is null) return NotFound();
        var session = Project(entity);

        var ninetyDaysAgo = _clock.Today.AddDays(-90);
        var roster = await _db.Bookings.AsNoTracking()
            .Where(b => b.ClassSessionId == id && b.Status != BookingStatus.Cancelled)
            .OrderBy(b => b.Status == BookingStatus.Waitlisted ? 1 : 0)
            .ThenBy(b => b.WaitlistPosition ?? 0).ThenBy(b => b.BookedAtUtc)
            .Select(b => new RosterEntry
            {
                BookingId = b.Id,
                MemberId = b.MemberId,
                MemberCode = b.Member.MemberCode,
                FullName = b.Member.FullName,
                PhotoUrl = b.Member.PhotoUrl,
                Phone = b.Member.Phone,
                Status = b.Status,
                WaitlistPosition = b.WaitlistPosition,
                CheckedInAtUtc = b.CheckedInAtUtc,
                BookedAtUtc = b.BookedAtUtc,
                WasPromoted = b.PromotedFromWaitlistAtUtc != null,
                NoShowsLast90Days = b.Member.Bookings.Count(x =>
                    x.Status == BookingStatus.NoShow && x.ClassSession.SessionDate >= ninetyDaysAgo)
            })
            .ToListAsync(ct);

        return Ok(new { session, roster });
    }

    [HttpPost("sessions/{id:int}/cancel")]
    public async Task<IActionResult> CancelSession(int id, CancelSessionRequest request, CancellationToken ct)
    {
        var session = await _db.ClassSessions
            .Include(s => s.ClassFormat)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        if (session is null) return NotFound();

        session.Status = SessionStatus.Cancelled;
        session.CancellationReason = request.Reason;
        session.UpdatedBy = User.Identity?.Name;

        var bookings = await _db.Bookings
            .Where(b => b.ClassSessionId == id
                     && (b.Status == BookingStatus.Booked || b.Status == BookingStatus.Waitlisted))
            .ToListAsync(ct);

        foreach (var booking in bookings)
        {
            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAtUtc = _clock.UtcNow;
            booking.Notes = $"Class cancelled: {request.Reason}";
            // A gym-side cancellation must never burn a class credit.
            if (booking.CreditConsumed && booking.SubscriptionId is { } subId)
            {
                var subscription = await _db.Subscriptions.FirstOrDefaultAsync(s => s.Id == subId, ct);
                if (subscription is not null) subscription.ClassCreditsRemaining += 1;
                booking.CreditConsumed = false;
            }
        }

        session.BookedCount = 0;
        session.WaitlistCount = 0;
        await _db.SaveChangesAsync(ct);

        if (request.NotifyMembers && bookings.Count > 0)
            await _notifier.SendManyAsync(bookings.Select(b => new OutboundMessage
            {
                MemberId = b.MemberId,
                Kind = NotificationKind.ClassCancelled,
                Title = $"{session.ClassFormat.Name} on {session.SessionDate:ddd dd MMM} is cancelled",
                Body = $"{request.Reason} Your credit has been returned — pick another slot and we will hold you a spot.",
                ActionUrl = "/portal/booking",
                Channels = new[] { NotificationChannel.InApp, NotificationChannel.WhatsApp }
            }), ct);

        _log.LogInformation("Session {SessionId} cancelled, {Count} booking(s) released", id, bookings.Count);
        return Ok(new { cancelledBookings = bookings.Count });
    }

    [HttpPost("sessions/{id:int}/substitute")]
    public async Task<IActionResult> Substitute(int id, [FromQuery] int? trainerId, CancellationToken ct)
    {
        var session = await _db.ClassSessions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (session is null) return NotFound();

        if (trainerId is null)
        {
            session.SubstituteTrainerId = null;
        }
        else
        {
            if (!await _db.Trainers.AnyAsync(t => t.Id == trainerId && t.IsActive, ct))
                return Problem("No such active coach.", statusCode: 400);

            // The substitute must be free at that hour, same as the rostered coach would be.
            var clash = await _db.ClassSessions.AnyAsync(s =>
                s.Id != id && s.Status == SessionStatus.Scheduled &&
                (s.TrainerId == trainerId || s.SubstituteTrainerId == trainerId) &&
                s.StartsAtUtc < session.EndsAtUtc && session.StartsAtUtc < s.EndsAtUtc, ct);

            if (clash) return Conflict(new ProblemDetails
            {
                Title = "Coach is busy",
                Detail = "That coach already has a class overlapping this slot.",
                Status = StatusCodes.Status409Conflict
            });

            session.SubstituteTrainerId = trainerId;
        }

        session.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ================================================================== bookings

    [HttpPost("bookings")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    public async Task<IActionResult> Book(BookMemberRequest request, CancellationToken ct)
    {
        var session = await _db.ClassSessions
            .Include(s => s.ClassSchedule)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, ct);
        if (session is null) return NotFound();
        if (session.Status != SessionStatus.Scheduled)
            return Problem("That class is not open for bookings.", statusCode: 400);

        var existing = await _db.Bookings.FirstOrDefaultAsync(b =>
            b.ClassSessionId == request.SessionId && b.MemberId == request.MemberId &&
            (b.Status == BookingStatus.Booked || b.Status == BookingStatus.Waitlisted), ct);
        if (existing is not null)
            return Conflict(new ProblemDetails { Title = "Already booked", Status = StatusCodes.Status409Conflict });

        var subscription = await _db.Subscriptions
            .Where(s => s.MemberId == request.MemberId && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.EndsOn)
            .FirstOrDefaultAsync(ct);

        var full = session.BookedCount >= session.Capacity;
        if (full && !request.AllowWaitlist)
            return Problem("The class is full.", statusCode: 409);
        if (full && !session.ClassSchedule.WaitlistEnabled)
            return Problem("The class is full and its waitlist is switched off.", statusCode: 409);
        if (full && session.WaitlistCount >= session.ClassSchedule.WaitlistCapacity)
            return Problem("The class and its waitlist are both full.", statusCode: 409);

        var booking = new Booking
        {
            ClassSessionId = session.Id,
            MemberId = request.MemberId,
            SubscriptionId = subscription?.Id,
            Status = full ? BookingStatus.Waitlisted : BookingStatus.Booked,
            BookedAtUtc = _clock.UtcNow,
            WaitlistPosition = full ? session.WaitlistCount + 1 : null,
            Notes = "Booked at the desk",
            CreatedBy = User.Identity?.Name
        };

        // Class packs bill by credit; an unlimited plan has none to spend.
        if (!full && subscription is { ClassCreditsRemaining: > 0 })
        {
            subscription.ClassCreditsRemaining -= 1;
            booking.CreditConsumed = true;
        }

        if (full) session.WaitlistCount++;
        else session.BookedCount++;

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync(ct);

        await _notifier.SendAsync(new OutboundMessage
        {
            MemberId = request.MemberId,
            Kind = full ? NotificationKind.General : NotificationKind.BookingConfirmed,
            Title = full ? "You are on the waitlist" : "Class booked",
            Body = full
                ? $"Position {booking.WaitlistPosition} for {session.SessionDate:ddd dd MMM} at {session.StartTime:HH\\:mm}."
                : $"{session.SessionDate:ddd dd MMM} at {session.StartTime:HH\\:mm}. See you on the floor.",
            ActionUrl = "/portal/booking"
        }, ct);

        return Created($"/api/admin/scheduling/bookings/{booking.Id}", new
        {
            booking.Id, status = booking.Status.ToString(), booking.WaitlistPosition,
            session.BookedCount, session.WaitlistCount
        });
    }

    /// <summary>Releases a spot and immediately promotes whoever is first on the waitlist.</summary>
    [HttpDelete("bookings/{id:int}")]
    public async Task<IActionResult> CancelBooking(int id, [FromQuery] string? reason, CancellationToken ct)
    {
        var booking = await _db.Bookings
            .Include(b => b.ClassSession)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null) return NotFound();
        if (booking.Status is BookingStatus.Cancelled or BookingStatus.Attended)
            return Problem("That booking is already closed.", statusCode: 400);

        var session = booking.ClassSession;
        var wasBooked = booking.Status == BookingStatus.Booked;

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAtUtc = _clock.UtcNow;
        booking.Notes = reason ?? "Cancelled at the desk";

        if (booking.CreditConsumed && booking.SubscriptionId is { } subId)
        {
            var subscription = await _db.Subscriptions.FirstOrDefaultAsync(s => s.Id == subId, ct);
            if (subscription is not null) subscription.ClassCreditsRemaining += 1;
            booking.CreditConsumed = false;
        }

        if (wasBooked) session.BookedCount = Math.Max(0, session.BookedCount - 1);
        else session.WaitlistCount = Math.Max(0, session.WaitlistCount - 1);

        await _db.SaveChangesAsync(ct);

        var promoted = await _scheduling.PromoteWaitlistAsync(session.Id, ct);
        if (promoted.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
            await _notifier.SendManyAsync(promoted.Select(b => new OutboundMessage
            {
                MemberId = b.MemberId,
                Kind = NotificationKind.WaitlistPromoted,
                Title = "A spot opened up",
                Body = $"You are in for {session.SessionDate:ddd dd MMM} at {session.StartTime:HH\\:mm}.",
                ActionUrl = "/portal/booking",
                Channels = new[] { NotificationChannel.InApp, NotificationChannel.WhatsApp }
            }), ct);
        }

        return Ok(new { promoted = promoted.Count, session.BookedCount, session.WaitlistCount });
    }

    /// <summary>Marks the roster after class — attended, or a no-show that feeds the churn score.</summary>
    [HttpPost("bookings/{id:int}/mark")]
    public async Task<IActionResult> Mark(int id, [FromQuery] BookingStatus status, CancellationToken ct)
    {
        if (status is not (BookingStatus.Attended or BookingStatus.NoShow or BookingStatus.Booked))
            return Problem("A roster mark is Attended, NoShow or Booked.", statusCode: 400);

        var booking = await _db.Bookings.Include(b => b.ClassSession).FirstOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null) return NotFound();

        var wasAttended = booking.Status == BookingStatus.Attended;
        booking.Status = status;
        booking.CheckedInAtUtc = status == BookingStatus.Attended ? _clock.UtcNow : null;

        var session = booking.ClassSession;
        if (status == BookingStatus.Attended && !wasAttended) session.AttendedCount++;
        if (status != BookingStatus.Attended && wasAttended) session.AttendedCount = Math.Max(0, session.AttendedCount - 1);

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>One include set shared by every session read, so the mapper never hits a null nav.</summary>
    private IQueryable<ClassSession> SessionQuery() => _db.ClassSessions
        .AsNoTracking()
        .Include(s => s.ClassFormat)
        .Include(s => s.Branch)
        .Include(s => s.Trainer)
        .Include(s => s.SubstituteTrainer)
        .Include(s => s.Room);

    private static ConflictRow Describe(ScheduleConflict c) => new()
    {
        Kind = c.Kind, Message = c.Message,
        ConflictingScheduleId = c.ConflictingScheduleId, ConflictingLabel = c.ConflictingLabel
    };

    private static AdminSessionRow Project(ClassSession s) => new()
    {
        Id = s.Id,
        Date = s.SessionDate.ToString("yyyy-MM-dd"),
        StartTime = s.StartTime.ToString("HH\\:mm"),
        EndTime = s.StartTime.AddMinutes(s.DurationMinutes).ToString("HH\\:mm"),
        FormatName = s.ClassFormat.Name,
        BranchName = s.Branch.Name,
        BranchId = s.BranchId,
        TrainerName = s.SubstituteTrainer != null ? s.SubstituteTrainer.FullName : s.Trainer.FullName,
        IsSubstitute = s.SubstituteTrainerId != null,
        RoomName = s.Room != null ? s.Room.Name : null,
        Capacity = s.Capacity,
        BookedCount = s.BookedCount,
        WaitlistCount = s.WaitlistCount,
        AttendedCount = s.AttendedCount,
        Status = s.Status,
        CancellationReason = s.CancellationReason,
        FillPercent = s.Capacity == 0 ? 0 : (int)Math.Round(s.BookedCount * 100d / s.Capacity)
    };

    private static void Apply(ClassFormat format, UpsertClassFormatRequest r)
    {
        format.Name = r.Name.Trim();
        format.ShortDescription = r.ShortDescription;
        format.Description = r.Description;
        format.DefaultDurationMinutes = r.DefaultDurationMinutes;
        format.DefaultCapacity = r.DefaultCapacity;
        format.Level = r.Level;
        format.Intensity = r.Intensity;
        format.EstimatedCalories = r.EstimatedCalories;
        format.CoverImageUrl = r.CoverImageUrl;
        format.IconKey = r.IconKey;
        format.Tags = r.Tags;
        format.ShowOnWebsite = r.ShowOnWebsite;
        format.IsActive = r.IsActive;
        format.DisplayOrder = r.DisplayOrder;
    }

    private static void Apply(ClassSchedule schedule, UpsertScheduleRequest r)
    {
        schedule.BranchId = r.BranchId;
        schedule.ClassFormatId = r.ClassFormatId;
        schedule.RoomId = r.RoomId;
        schedule.TrainerId = r.TrainerId;
        schedule.DayOfWeek = r.DayOfWeek;
        schedule.StartTime = r.StartTime;
        schedule.DurationMinutes = r.DurationMinutes;
        schedule.Capacity = r.Capacity;
        schedule.EffectiveFrom = r.EffectiveFrom;
        schedule.EffectiveTo = r.EffectiveTo;
        schedule.BookingOpensHoursBefore = r.BookingOpensHoursBefore;
        schedule.CancelCutoffHoursBefore = r.CancelCutoffHoursBefore;
        schedule.WaitlistEnabled = r.WaitlistEnabled;
        schedule.WaitlistCapacity = r.WaitlistCapacity;
        schedule.IsActive = r.IsActive;
    }
}
