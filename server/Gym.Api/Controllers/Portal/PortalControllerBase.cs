using System.Security.Claims;
using Gym.Api.Contracts;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers.Portal;

/// <summary>
/// Base for every member-portal controller.
///
/// The member id comes from the <c>member_id</c> claim and from nowhere else. No portal
/// endpoint accepts a member id in its route or body, so "own data only" (04 §4) is a
/// property of the surface rather than a check each handler has to remember to make.
/// </summary>
[ApiController]
[Authorize(Roles = RoleNames.Member)]
[Produces("application/json")]
public abstract class PortalControllerBase : ControllerBase
{
    /// <summary>The whole product runs on IST; sessions store both the wall clock and the instant.</summary>
    protected static readonly TimeSpan IstOffset = TimeSpan.FromMinutes(330);

    /// <summary>
    /// The signed-in member. A Member-role token with no member row is a broken account
    /// rather than an unauthorised one, so it answers 403 with an explanation.
    /// </summary>
    protected int? CurrentMemberId
    {
        get
        {
            var raw = User.FindFirstValue(GymClaims.MemberId);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    protected ActionResult NoMemberProfile() => StatusCode(
        StatusCodes.Status403Forbidden,
        new ProblemDetails
        {
            Title = "No member profile",
            Detail = "This login is not linked to a member record. The front desk can link it in one step.",
            Status = StatusCodes.Status403Forbidden
        });

    protected static string Level(ClassLevel level) => level switch
    {
        ClassLevel.AllLevels => "All levels",
        _ => level.ToString()
    };

    /// <summary>Buckets match the public timetable's so one vocabulary runs across both surfaces.</summary>
    protected static string TimeBucket(TimeOnly start) => start.Hour switch
    {
        < 8 => "Early morning",
        < 11 => "Morning",
        < 16 => "Midday",
        < 19 => "Evening",
        _ => "Late evening"
    };

    protected static string Spaced(string pascalCase) =>
        System.Text.RegularExpressions.Regex.Replace(pascalCase, "(?<!^)([A-Z])", " $1");
}

/// <summary>
/// Everything the booking rules need about one member, read once per request: which plan they
/// hold, where it is valid, and how many class credits are left. Booking is refused for a
/// reason the member can act on, never with a disabled button and no explanation.
/// </summary>
public record MemberAccess(
    Member Member,
    Subscription? Subscription,
    string? BlockedReason,
    int? ClassCreditsRemaining)
{
    public bool CanBookAtAll => BlockedReason is null;

    public bool CoversBranch(int branchId) =>
        Subscription is null
            ? false
            : Subscription.Plan.AccessScope == AccessScope.AllBranches ||
              Subscription.BranchId == branchId ||
              Member.HomeBranchId == branchId;
}

/// <summary>Shared session reads and booking-rule evaluation for the portal.</summary>
public static class PortalSessions
{
    /// <summary>
    /// Resolves the membership the member would book against and why they might not be able to.
    /// Dues are deliberately not a block: an unpaid invoice is a conversation, not a locked door
    /// — the same rule the desk kiosk applies.
    /// </summary>
    public static async Task<MemberAccess> LoadAccessAsync(
        GymDbContext db, int memberId, DateOnly today, CancellationToken ct)
    {
        var member = await db.Members
            .Include(m => m.HomeBranch)
            .FirstAsync(m => m.Id == memberId, ct);

        var subscription = await db.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.MemberId == memberId)
            .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Frozen)
            .OrderByDescending(s => s.EndsOn)
            .FirstOrDefaultAsync(ct);

        string? blocked = subscription switch
        {
            null => "You need an active membership to book. Pick a plan and you can book straight away.",
            { Status: SubscriptionStatus.Frozen } s =>
                $"Your membership is frozen until {s.FreezeEndsOn:dd MMM yyyy}. Resume it to start booking again.",
            { } s when s.EndsOn < today =>
                $"Your membership ended on {s.EndsOn:dd MMM yyyy}. Renew it to book.",
            _ => null
        };

        int? credits = subscription is null ? null
            : subscription.Plan.Kind is PlanKind.ClassPack ? subscription.ClassCreditsRemaining
            : null;

        if (blocked is null && credits == 0)
            blocked = "Your class pack is out of credits. Buy another pack to keep booking.";

        return new MemberAccess(member, subscription, blocked, credits);
    }

    /// <summary>
    /// Reduces a member's bookings to one per session.
    ///
    /// A member can legitimately hold more than one row against the same session — a cancelled
    /// booking and a later re-book, or a waitlist entry that was promoted — so keying a
    /// dictionary straight off the session id throws the moment history is not pristine. The
    /// live commitment wins; failing that, the most recent row.
    /// </summary>
    public static Dictionary<int, Booking> OnePerSession(IEnumerable<Booking> bookings) =>
        bookings
            .GroupBy(b => b.ClassSessionId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(b => b.Status switch
                        {
                            BookingStatus.Booked => 0,
                            BookingStatus.Waitlisted => 1,
                            BookingStatus.Attended => 2,
                            BookingStatus.NoShow => 3,
                            _ => 4
                        })
                     .ThenByDescending(b => b.BookedAtUtc)
                     .First());

    /// <summary>One include set every portal session read shares, so no mapper ever hits a null nav.</summary>
    public static IQueryable<ClassSession> Query(GymDbContext db) => db.ClassSessions
        .AsNoTracking()
        .Include(s => s.ClassFormat)
        .Include(s => s.Branch)
        .Include(s => s.Trainer)
        .Include(s => s.SubstituteTrainer)
        .Include(s => s.Room)
        .Include(s => s.ClassSchedule);

    /// <summary>
    /// Maps a session with the member's own relationship to it folded in. The booking window,
    /// the cancel cut-off and the waitlist policy all come off the schedule rule rather than
    /// being hard-coded, because the owner sets them per slot in the admin panel.
    /// </summary>
    public static PortalSessionResponse Map(
        ClassSession s, Booking? mine, MemberAccess access, DateTime nowUtc)
    {
        var schedule = s.ClassSchedule;
        var opensAt = s.StartsAtUtc.AddHours(-schedule.BookingOpensHoursBefore);
        var cutoffAt = s.StartsAtUtc.AddHours(-schedule.CancelCutoffHoursBefore);

        var spotsLeft = Math.Max(0, s.Capacity - s.BookedCount);
        var isFull = spotsLeft == 0;
        var hasActiveBooking = mine is { Status: BookingStatus.Booked or BookingStatus.Waitlisted };

        string? blocked = null;
        if (s.Status == SessionStatus.Cancelled) blocked = "This class was cancelled.";
        else if (s.Status == SessionStatus.Completed || s.StartsAtUtc <= nowUtc) blocked = "This class has already run.";
        else if (access.BlockedReason is { } reason) blocked = reason;
        else if (!access.CoversBranch(s.BranchId))
            blocked = $"Your plan covers {access.Member.HomeBranch.Name} only.";
        else if (nowUtc < opensAt)
            blocked = $"Booking opens {schedule.BookingOpensHoursBefore} hours before — {(opensAt + IstOffsetSpan):ddd dd MMM, HH\\:mm}.";

        var openForBooking = blocked is null && !hasActiveBooking;
        var canBook = openForBooking && !isFull;
        var canJoinWaitlist = openForBooking && isFull && schedule.WaitlistEnabled
                              && s.WaitlistCount < schedule.WaitlistCapacity;

        return new PortalSessionResponse
        {
            Id = s.Id,
            Date = s.SessionDate.ToString("yyyy-MM-dd"),
            StartTime = s.StartTime.ToString("HH\\:mm"),
            EndTime = s.StartTime.AddMinutes(s.DurationMinutes).ToString("HH\\:mm"),
            StartsAtUtc = s.StartsAtUtc,
            DurationMinutes = s.DurationMinutes,
            FormatName = s.ClassFormat.Name,
            FormatSlug = s.ClassFormat.Slug,
            IconKey = s.ClassFormat.IconKey,
            CoverImageUrl = s.ClassFormat.CoverImageUrl ?? $"/media/classes/{s.ClassFormat.Slug}.jpg",
            LevelName = s.ClassFormat.Level == ClassLevel.AllLevels ? "All levels" : s.ClassFormat.Level.ToString(),
            EstimatedCalories = s.ClassFormat.EstimatedCalories,
            BranchId = s.BranchId,
            BranchName = s.Branch.Name,
            BranchSlug = s.Branch.Slug,
            TrainerName = (s.SubstituteTrainer ?? s.Trainer).FullName,
            TrainerSlug = (s.SubstituteTrainer ?? s.Trainer).Slug,
            TrainerPortraitUrl = (s.SubstituteTrainer ?? s.Trainer).PortraitUrl
                ?? $"/media/trainers/{(s.SubstituteTrainer ?? s.Trainer).Slug}.jpg",
            IsSubstitute = s.SubstituteTrainerId is not null,
            RoomName = s.Room?.Name,
            Capacity = s.Capacity,
            BookedCount = s.BookedCount,
            SpotsLeft = spotsLeft,
            WaitlistCount = s.WaitlistCount,
            Status = s.Status,
            TimeOfDay = s.StartTime.Hour switch
            {
                < 8 => "Early morning",
                < 11 => "Morning",
                < 16 => "Midday",
                < 19 => "Evening",
                _ => "Late evening"
            },
            MyBookingId = hasActiveBooking ? mine!.Id : null,
            MyBookingStatus = mine?.Status,
            MyWaitlistPosition = mine?.WaitlistPosition,
            CanBook = canBook,
            CanJoinWaitlist = canJoinWaitlist,
            CanCancel = hasActiveBooking && s.StartsAtUtc > nowUtc && s.Status == SessionStatus.Scheduled,
            BlockedReason = hasActiveBooking ? null : blocked,
            BookingOpensAtUtc = opensAt,
            CancelCutoffAtUtc = cutoffAt,
            IsLateCancelWindow = nowUtc > cutoffAt,
            MyRating = mine?.RatingScore
        };
    }

    private static TimeSpan IstOffsetSpan => TimeSpan.FromMinutes(330);
}
