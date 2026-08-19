using Gym.Api.Contracts;
using Gym.Api.Hubs;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers.Portal;

/// <summary>
/// Booking from the member's side (Module 3 — Booking): the filterable timetable with the
/// member's own state folded into every card, one-tap book, waitlist join with auto-promotion,
/// cancel, and the post-class rating prompt.
///
/// The optimistic UI on the client is only safe because this endpoint is authoritative about
/// capacity: the response always carries the session's real counts back, so a card that guessed
/// wrong corrects itself on the same round trip rather than staying wrong until a refetch.
/// </summary>
[Route("api/portal")]
public class PortalBookingController : PortalControllerBase
{
    private readonly GymDbContext _db;
    private readonly SchedulingService _scheduling;
    private readonly INotificationDispatcher _notifier;
    private readonly IHubContext<OccupancyHub, IOccupancyClient> _hub;
    private readonly IClock _clock;
    private readonly ILogger<PortalBookingController> _log;

    public PortalBookingController(
        GymDbContext db, SchedulingService scheduling, INotificationDispatcher notifier,
        IHubContext<OccupancyHub, IOccupancyClient> hub, IClock clock, ILogger<PortalBookingController> log)
    {
        _db = db;
        _scheduling = scheduling;
        _notifier = notifier;
        _hub = hub;
        _clock = clock;
        _log = log;
    }

    /// <summary>The bookable timetable, with facets that only ever offer live combinations.</summary>
    [HttpGet("timetable")]
    [ProducesResponseType(typeof(PortalTimetableResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PortalTimetableResponse>> Timetable(
        [FromQuery] int? branchId,
        [FromQuery] string? formatSlug,
        [FromQuery] string? trainerSlug,
        [FromQuery] string? timeOfDay,
        [FromQuery] DateOnly? from,
        [FromQuery] int days = 7,
        CancellationToken ct = default)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var today = _clock.Today;
        var nowUtc = _clock.UtcNow;
        var start = from is { } requested && requested > today ? requested : today;
        var span = Math.Clamp(days, 1, 21);
        var end = start.AddDays(span - 1);

        var access = await PortalSessions.LoadAccessAsync(_db, memberId, today, ct);

        var sessions = await PortalSessions.Query(_db)
            .Where(s => s.SessionDate >= start && s.SessionDate <= end)
            .Where(s => s.Status != SessionStatus.Cancelled)
            .Where(s => branchId == null || s.BranchId == branchId)
            .Where(s => formatSlug == null || s.ClassFormat.Slug == formatSlug)
            .Where(s => trainerSlug == null || s.Trainer.Slug == trainerSlug ||
                        (s.SubstituteTrainer != null && s.SubstituteTrainer.Slug == trainerSlug))
            .OrderBy(s => s.StartsAtUtc)
            .ToListAsync(ct);

        var sessionIds = sessions.Select(s => s.Id).ToList();
        var mine = PortalSessions.OnePerSession(await _db.Bookings
            .AsNoTracking()
            .Where(b => b.MemberId == memberId && sessionIds.Contains(b.ClassSessionId))
            .Where(b => b.Status != BookingStatus.Cancelled)
            .ToListAsync(ct));

        var mapped = sessions
            .Select(s => PortalSessions.Map(s, mine.GetValueOrDefault(s.Id), access, nowUtc))
            .ToList();

        // Time-of-day is a derived bucket, so it filters after projection like the public one.
        if (!string.IsNullOrWhiteSpace(timeOfDay))
            mapped = mapped.Where(s => string.Equals(s.TimeOfDay, timeOfDay, StringComparison.OrdinalIgnoreCase)).ToList();

        var branchCounts = await PortalSessions.Query(_db)
            .Where(s => s.SessionDate >= start && s.SessionDate <= end && s.Status != SessionStatus.Cancelled)
            .GroupBy(s => new { s.BranchId, s.Branch.Name, s.Branch.Slug })
            .Select(g => new { g.Key.BranchId, g.Key.Name, g.Key.Slug, Count = g.Count() })
            .ToListAsync(ct);

        return Ok(new PortalTimetableResponse
        {
            FromDate = start.ToString("yyyy-MM-dd"),
            ToDate = end.ToString("yyyy-MM-dd"),
            Sessions = mapped,
            Formats = mapped
                .GroupBy(s => (s.FormatSlug, s.FormatName))
                .Select(g => new TimetableFilterOption { Slug = g.Key.FormatSlug, Name = g.Key.FormatName, Count = g.Count() })
                .OrderBy(o => o.Name).ToList(),
            Trainers = mapped
                .GroupBy(s => (s.TrainerSlug, s.TrainerName))
                .Select(g => new TimetableFilterOption { Slug = g.Key.TrainerSlug, Name = g.Key.TrainerName, Count = g.Count() })
                .OrderBy(o => o.Name).ToList(),
            Branches = branchCounts
                .Select(b => new PortalBranchOption
                {
                    Id = b.BranchId,
                    Name = b.Name,
                    Slug = b.Slug,
                    Count = b.Count,
                    IsHome = b.BranchId == access.Member.HomeBranchId
                })
                .OrderByDescending(b => b.IsHome).ThenBy(b => b.Name).ToList(),
            BookingBlockedReason = access.BlockedReason,
            ClassCreditsRemaining = access.ClassCreditsRemaining
        });
    }

    /// <summary>Upcoming and past bookings — the "my classes" side of the booking screen.</summary>
    [HttpGet("bookings")]
    [ProducesResponseType(typeof(IReadOnlyList<PortalBookingRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PortalBookingRow>>> Bookings(
        [FromQuery] string scope = "upcoming", [FromQuery] int take = 40, CancellationToken ct = default)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var nowUtc = _clock.UtcNow;
        var upcoming = !string.Equals(scope, "past", StringComparison.OrdinalIgnoreCase);

        var query = _db.Bookings
            .AsNoTracking()
            .Include(b => b.ClassSession).ThenInclude(s => s.ClassFormat)
            .Include(b => b.ClassSession).ThenInclude(s => s.Branch)
            .Include(b => b.ClassSession).ThenInclude(s => s.Trainer)
            .Include(b => b.ClassSession).ThenInclude(s => s.SubstituteTrainer)
            .Include(b => b.ClassSession).ThenInclude(s => s.Room)
            .Include(b => b.ClassSession).ThenInclude(s => s.ClassSchedule)
            .Where(b => b.MemberId == memberId);

        query = upcoming
            ? query.Where(b => b.ClassSession.StartsAtUtc >= nowUtc && b.Status != BookingStatus.Cancelled)
                   .OrderBy(b => b.ClassSession.StartsAtUtc)
            : query.Where(b => b.ClassSession.StartsAtUtc < nowUtc)
                   .OrderByDescending(b => b.ClassSession.StartsAtUtc);

        var rows = await query.Take(Math.Clamp(take, 1, 120)).ToListAsync(ct);
        return Ok(rows.Select(b => Describe(b, nowUtc)).ToList());
    }

    /// <summary>Classes that ran and have not been rated yet — the post-class prompt's source.</summary>
    [HttpGet("rating-prompts")]
    [ProducesResponseType(typeof(IReadOnlyList<PortalRatingPrompt>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PortalRatingPrompt>>> RatingPrompts(CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();
        return Ok(await LoadRatingPromptsAsync(_db, memberId, _clock.UtcNow, ct));
    }

    /// <summary>
    /// One-tap book. Full and waitlisted are two outcomes of the same call, so the client never
    /// has to ask "is it full?" first and race someone else between the two requests.
    /// </summary>
    [HttpPost("bookings")]
    [ProducesResponseType(typeof(PortalBookingResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<PortalBookingResponse>> Book(PortalBookRequest request, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var today = _clock.Today;
        var nowUtc = _clock.UtcNow;

        var session = await _db.ClassSessions
            .Include(s => s.ClassSchedule)
            .Include(s => s.ClassFormat)
            .Include(s => s.Branch)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, ct);
        if (session is null) return NotFound();

        if (session.Status != SessionStatus.Scheduled)
            return Problem("That class is not open for bookings.", statusCode: StatusCodes.Status400BadRequest);
        if (session.StartsAtUtc <= nowUtc)
            return Problem("That class has already started.", statusCode: StatusCodes.Status400BadRequest);

        var access = await PortalSessions.LoadAccessAsync(_db, memberId, today, ct);
        if (access.BlockedReason is { } blocked)
            return Problem(blocked, statusCode: StatusCodes.Status402PaymentRequired);
        if (!access.CoversBranch(session.BranchId))
            return Problem($"Your plan covers {access.Member.HomeBranch.Name} only.",
                statusCode: StatusCodes.Status403Forbidden);

        var opensAt = session.StartsAtUtc.AddHours(-session.ClassSchedule.BookingOpensHoursBefore);
        if (nowUtc < opensAt)
            return Problem(
                $"Booking opens {session.ClassSchedule.BookingOpensHoursBefore} hours before the class.",
                statusCode: StatusCodes.Status400BadRequest);

        var existing = await _db.Bookings.FirstOrDefaultAsync(b =>
            b.ClassSessionId == session.Id && b.MemberId == memberId &&
            (b.Status == BookingStatus.Booked || b.Status == BookingStatus.Waitlisted), ct);
        if (existing is not null)
            return Conflict(new ProblemDetails
            {
                Title = "Already booked",
                Detail = existing.Status == BookingStatus.Waitlisted
                    ? $"You are already number {existing.WaitlistPosition} on the waitlist for this class."
                    : "You already have a spot in this class.",
                Status = StatusCodes.Status409Conflict
            });

        // A member cannot be in two rooms at once; a double-booked slot is a no-show waiting to happen.
        var clash = await _db.Bookings
            .Include(b => b.ClassSession).ThenInclude(s => s.ClassFormat)
            .Where(b => b.MemberId == memberId && b.Status == BookingStatus.Booked)
            .Where(b => b.ClassSession.StartsAtUtc < session.EndsAtUtc && session.StartsAtUtc < b.ClassSession.EndsAtUtc)
            .FirstOrDefaultAsync(ct);
        if (clash is not null)
            return Conflict(new ProblemDetails
            {
                Title = "That overlaps another booking",
                Detail = $"You are already booked into {clash.ClassSession.ClassFormat.Name} at " +
                         $"{clash.ClassSession.StartTime:HH\\:mm} on {clash.ClassSession.SessionDate:ddd dd MMM}.",
                Status = StatusCodes.Status409Conflict
            });

        var isFull = session.BookedCount >= session.Capacity;
        if (isFull && !request.AllowWaitlist)
            return Problem("The class is full.", statusCode: StatusCodes.Status409Conflict);
        if (isFull && !session.ClassSchedule.WaitlistEnabled)
            return Problem("The class is full and its waitlist is switched off.", statusCode: StatusCodes.Status409Conflict);
        if (isFull && session.WaitlistCount >= session.ClassSchedule.WaitlistCapacity)
            return Problem("The class and its waitlist are both full.", statusCode: StatusCodes.Status409Conflict);

        var booking = new Booking
        {
            ClassSessionId = session.Id,
            MemberId = memberId,
            SubscriptionId = access.Subscription?.Id,
            Status = isFull ? BookingStatus.Waitlisted : BookingStatus.Booked,
            BookedAtUtc = nowUtc,
            WaitlistPosition = isFull ? session.WaitlistCount + 1 : null,
            Notes = "Booked in the member app",
            CreatedBy = access.Member.MemberCode
        };

        // Packs bill by credit; an unlimited plan has none to spend.
        if (!isFull && access.Subscription is { ClassCreditsRemaining: > 0, Plan.Kind: PlanKind.ClassPack })
        {
            access.Subscription.ClassCreditsRemaining -= 1;
            booking.CreditConsumed = true;
        }

        if (isFull) session.WaitlistCount++;
        else session.BookedCount++;

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync(ct);

        await _notifier.SendAsync(new OutboundMessage
        {
            MemberId = memberId,
            Kind = isFull ? NotificationKind.General : NotificationKind.BookingConfirmed,
            Title = isFull ? "You are on the waitlist" : "Class booked",
            Body = isFull
                ? $"Position {booking.WaitlistPosition} for {session.ClassFormat.Name} on " +
                  $"{session.SessionDate:ddd dd MMM} at {session.StartTime:HH\\:mm}. We will tell you the moment a spot frees up."
                : $"{session.ClassFormat.Name} · {session.SessionDate:ddd dd MMM} at {session.StartTime:HH\\:mm} · " +
                  $"{session.Branch.Name}. See you on the floor.",
            ActionUrl = "/portal/book",
            TemplateKey = isFull ? "booking.waitlisted" : "booking.confirmed"
        }, ct);

        await BroadcastCapacityAsync(session, ct);

        _log.LogInformation("{Member} {Action} session {SessionId}",
            access.Member.MemberCode, isFull ? "waitlisted for" : "booked", session.Id);

        var creditsLeft = access.Subscription?.Plan.Kind == PlanKind.ClassPack
            ? access.Subscription.ClassCreditsRemaining
            : (int?)null;

        return Created($"/api/portal/bookings/{booking.Id}", new PortalBookingResponse
        {
            BookingId = booking.Id,
            Status = booking.Status,
            StatusName = booking.Status.ToString(),
            WaitlistPosition = booking.WaitlistPosition,
            BookedCount = session.BookedCount,
            Capacity = session.Capacity,
            SpotsLeft = Math.Max(0, session.Capacity - session.BookedCount),
            WaitlistCount = session.WaitlistCount,
            Headline = isFull ? $"Waitlisted — number {booking.WaitlistPosition}" : "You are in",
            Message = isFull
                ? "We will move you up automatically and tell you the moment a spot opens."
                : $"{session.ClassFormat.Name} · {session.SessionDate:ddd dd MMM} at {session.StartTime:HH\\:mm}.",
            ClassCreditsRemaining = creditsLeft
        });
    }

    /// <summary>
    /// Cancels the member's own booking and immediately promotes whoever is first on the
    /// waitlist. Inside the cut-off it still cancels — a late cancel is recorded, not refused,
    /// because a member who cannot cancel simply does not turn up and the spot is lost anyway.
    /// </summary>
    [HttpDelete("bookings/{id:int}")]
    [ProducesResponseType(typeof(PortalBookingResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PortalBookingResponse>> Cancel(
        int id, [FromQuery] string? reason, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var booking = await _db.Bookings
            .Include(b => b.ClassSession).ThenInclude(s => s.ClassSchedule)
            .Include(b => b.ClassSession).ThenInclude(s => s.ClassFormat)
            .FirstOrDefaultAsync(b => b.Id == id && b.MemberId == memberId, ct);
        if (booking is null) return NotFound();

        if (booking.Status is BookingStatus.Cancelled or BookingStatus.Attended)
            return Problem("That booking is already closed.", statusCode: StatusCodes.Status400BadRequest);

        var session = booking.ClassSession;
        var nowUtc = _clock.UtcNow;
        if (session.StartsAtUtc <= nowUtc)
            return Problem("That class has already started.", statusCode: StatusCodes.Status400BadRequest);

        var cutoff = session.StartsAtUtc.AddHours(-session.ClassSchedule.CancelCutoffHoursBefore);
        var late = nowUtc > cutoff;
        var wasBooked = booking.Status == BookingStatus.Booked;

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAtUtc = nowUtc;
        booking.WasLateCancel = late && wasBooked;
        booking.WaitlistPosition = null;
        booking.Notes = reason ?? "Cancelled in the member app";

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
                Body = $"You are in for {session.ClassFormat.Name} on {session.SessionDate:ddd dd MMM} " +
                       $"at {session.StartTime:HH\\:mm}.",
                ActionUrl = "/portal/book",
                TemplateKey = "booking.promoted",
                Channels = new[] { NotificationChannel.InApp, NotificationChannel.WhatsApp }
            }), ct);

            foreach (var b in promoted)
                await _hub.Clients.All.WaitlistPromoted(new WaitlistPromotion
                {
                    BookingId = b.Id,
                    ClassSessionId = session.Id,
                    MemberId = b.MemberId,
                    ClassName = session.ClassFormat.Name,
                    StartsAtUtc = session.StartsAtUtc
                });
        }

        await BroadcastCapacityAsync(session, ct);

        return Ok(new PortalBookingResponse
        {
            BookingId = booking.Id,
            Status = booking.Status,
            StatusName = booking.Status.ToString(),
            WaitlistPosition = null,
            BookedCount = session.BookedCount,
            Capacity = session.Capacity,
            SpotsLeft = Math.Max(0, session.Capacity - session.BookedCount),
            WaitlistCount = session.WaitlistCount,
            Headline = "Cancelled",
            Message = late
                ? "Cancelled inside the free window, so it is recorded as a late cancel."
                : promoted.Count > 0
                    ? "Your spot went straight to the next member on the waitlist."
                    : "The spot is back on the timetable.",
            ClassCreditsRemaining = null
        });
    }

    /// <summary>
    /// Post-class rating. The score lands on the booking (so the roster shows it) and on the
    /// coach's running average in one write, which is what the trainer performance view reads.
    /// </summary>
    [HttpPost("bookings/{id:int}/rate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Rate(int id, PortalRateRequest request, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        if (request.Score is < 1 or > 5)
            return Problem("A rating is between 1 and 5.", statusCode: StatusCodes.Status400BadRequest);

        var booking = await _db.Bookings
            .Include(b => b.ClassSession).ThenInclude(s => s.Trainer)
            .FirstOrDefaultAsync(b => b.Id == id && b.MemberId == memberId, ct);
        if (booking is null) return NotFound();

        if (booking.ClassSession.StartsAtUtc > _clock.UtcNow)
            return Problem("That class has not run yet.", statusCode: StatusCodes.Status400BadRequest);
        if (booking.RatingScore is not null)
            return Problem("You have already rated this class.", statusCode: StatusCodes.Status409Conflict);

        booking.RatingScore = request.Score;
        booking.RatingComment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();

        var trainerId = booking.ClassSession.SubstituteTrainerId ?? booking.ClassSession.TrainerId;
        var trainer = await _db.Trainers.FirstAsync(t => t.Id == trainerId, ct);

        _db.TrainerRatings.Add(new TrainerRating
        {
            TrainerId = trainerId,
            MemberId = memberId,
            ClassSessionId = booking.ClassSessionId,
            Score = request.Score,
            Comment = booking.RatingComment,
            // A comment is a testimonial the owner may want to publish; a bare score is not.
            IsPublished = false
        });

        // Running average kept on the trainer so the public card never has to aggregate.
        var total = trainer.AverageRating * trainer.RatingCount + request.Score;
        trainer.RatingCount += 1;
        trainer.AverageRating = decimal.Round(total / trainer.RatingCount, 2);

        await _db.SaveChangesAsync(ct);

        // A low score is worth the desk's attention the same day, not in a monthly report.
        if (request.Score <= 2)
        {
            var member = await _db.Members.AsNoTracking().FirstAsync(m => m.Id == memberId, ct);
            await _notifier.SendAsync(new OutboundMessage
            {
                Kind = NotificationKind.General,
                Title = $"{request.Score}-star rating for {trainer.FullName}",
                Body = $"{member.FullName} ({member.MemberCode}) rated a class {request.Score}/5." +
                       (booking.RatingComment is null ? "" : $" \"{booking.RatingComment}\""),
                ActionUrl = $"/admin/members/{memberId}",
                TemplateKey = "feedback.low-rating"
            }, ct);
        }

        return NoContent();
    }

    // ---------------------------------------------------------------- helpers

    internal static async Task<List<PortalRatingPrompt>> LoadRatingPromptsAsync(
        GymDbContext db, int memberId, DateTime nowUtc, CancellationToken ct)
    {
        // Only classes they actually attended, and only for a week — a prompt about a class
        // from last month is noise, and nobody remembers it well enough to answer honestly.
        var since = nowUtc.AddDays(-7);

        var rows = await db.Bookings
            .AsNoTracking()
            .Include(b => b.ClassSession).ThenInclude(s => s.ClassFormat)
            .Include(b => b.ClassSession).ThenInclude(s => s.Trainer)
            .Include(b => b.ClassSession).ThenInclude(s => s.SubstituteTrainer)
            .Where(b => b.MemberId == memberId && b.RatingScore == null)
            .Where(b => b.Status == BookingStatus.Attended)
            .Where(b => b.ClassSession.EndsAtUtc < nowUtc && b.ClassSession.EndsAtUtc > since)
            .OrderByDescending(b => b.ClassSession.StartsAtUtc)
            .Take(5)
            .ToListAsync(ct);

        return rows.Select(b =>
        {
            var coach = b.ClassSession.SubstituteTrainer ?? b.ClassSession.Trainer;
            return new PortalRatingPrompt
            {
                BookingId = b.Id,
                SessionId = b.ClassSessionId,
                FormatName = b.ClassSession.ClassFormat.Name,
                TrainerId = coach.Id,
                TrainerName = coach.FullName,
                TrainerPortraitUrl = coach.PortraitUrl ?? $"/media/trainers/{coach.Slug}.jpg",
                Date = b.ClassSession.SessionDate.ToString("yyyy-MM-dd"),
                StartTime = b.ClassSession.StartTime.ToString("HH\\:mm")
            };
        }).ToList();
    }

    private PortalBookingRow Describe(Booking b, DateTime nowUtc)
    {
        var s = b.ClassSession;
        var coach = s.SubstituteTrainer ?? s.Trainer;
        var cutoff = s.StartsAtUtc.AddHours(-s.ClassSchedule.CancelCutoffHoursBefore);
        var active = b.Status is BookingStatus.Booked or BookingStatus.Waitlisted;

        return new PortalBookingRow
        {
            Id = b.Id,
            SessionId = b.ClassSessionId,
            Status = b.Status,
            StatusName = b.Status.ToString(),
            WaitlistPosition = b.WaitlistPosition,
            Date = s.SessionDate.ToString("yyyy-MM-dd"),
            StartTime = s.StartTime.ToString("HH\\:mm"),
            StartsAtUtc = s.StartsAtUtc,
            DurationMinutes = s.DurationMinutes,
            FormatName = s.ClassFormat.Name,
            FormatSlug = s.ClassFormat.Slug,
            CoverImageUrl = s.ClassFormat.CoverImageUrl ?? $"/media/classes/{s.ClassFormat.Slug}.jpg",
            TrainerName = coach.FullName,
            TrainerSlug = coach.Slug,
            TrainerPortraitUrl = coach.PortraitUrl ?? $"/media/trainers/{coach.Slug}.jpg",
            BranchName = s.Branch.Name,
            RoomName = s.Room?.Name,
            CanCancel = active && s.StartsAtUtc > nowUtc && s.Status == SessionStatus.Scheduled,
            IsLateCancelWindow = nowUtc > cutoff,
            CanRate = b.Status == BookingStatus.Attended && b.RatingScore is null && s.EndsAtUtc < nowUtc,
            RatingScore = b.RatingScore,
            RatingComment = b.RatingComment,
            CheckedInAtUtc = b.CheckedInAtUtc
        };
    }

    /// <summary>
    /// Pushes the new counts to anyone watching this session. The capacity ring on another
    /// member's screen moves as spots go, which is the whole point of a spots-left number.
    /// </summary>
    private async Task BroadcastCapacityAsync(ClassSession session, CancellationToken ct)
    {
        await _hub.Clients.All.SessionCapacityChanged(new SessionCapacityUpdate
        {
            ClassSessionId = session.Id,
            BookedCount = session.BookedCount,
            Capacity = session.Capacity,
            WaitlistCount = session.WaitlistCount,
            SpotsLeft = Math.Max(0, session.Capacity - session.BookedCount)
        });
    }
}
