using Gym.Api.Contracts;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.BackgroundJobs;
using Gym.Infrastructure.Services;
using Gym.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers.Admin;

/// <summary>
/// Attendance: the QR kiosk at the desk, manual check-in and check-out, the peak-hours
/// heatmap and the absentee list that feeds win-back.
///
/// The kiosk answers in one round trip with everything the person at the desk needs to act
/// on — plan, expiry, dues and today's classes — because a turnstile that only says "no" is
/// a turnstile someone has to work around.
/// </summary>
[ApiController]
[Route("api/admin/attendance")]
[Authorize(Roles = RoleNames.Admin)]
[Produces("application/json")]
public class AttendanceController : ControllerBase
{
    private readonly GymDbContext _db;
    private readonly INotificationDispatcher _notifier;
    private readonly IClock _clock;
    private readonly OccupancyService _occupancy;
    private readonly IRealtimeNotifier _realtime;
    private readonly ILogger<AttendanceController> _log;

    public AttendanceController(
        GymDbContext db, INotificationDispatcher notifier, IClock clock,
        OccupancyService occupancy, IRealtimeNotifier realtime, ILogger<AttendanceController> log)
    {
        _db = db;
        _notifier = notifier;
        _clock = clock;
        _occupancy = occupancy;
        _realtime = realtime;
        _log = log;
    }

    /// <summary>
    /// Recomputes and pushes the branch's meter. Every path that changes who is on the floor
    /// calls this, so the public gauge, the portal home and the dashboard move together.
    /// </summary>
    private async Task PushOccupancyAsync(int branchId, CancellationToken ct)
    {
        var snapshot = await _occupancy.ForBranchAsync(branchId, ct);
        if (snapshot is not null) await _realtime.OccupancyChangedAsync(snapshot, ct);
    }

    /// <summary>Desk search — code, name or the last digits of a number, all in one box.</summary>
    [HttpGet("lookup")]
    public async Task<IActionResult> Lookup([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return Ok(Array.Empty<object>());

        var term = q.Trim();
        var matches = await _db.Members.AsNoTracking()
            .Where(m => m.MemberCode.Contains(term) || m.FullName.Contains(term) || m.Phone.Contains(term))
            .OrderBy(m => m.FullName)
            .Take(10)
            .Select(m => new
            {
                m.Id, m.MemberCode, m.FullName, m.Phone, m.PhotoUrl,
                BranchName = m.HomeBranch.Name, Status = m.Status.ToString()
            })
            .ToListAsync(ct);

        return Ok(matches);
    }

    [HttpPost("checkin")]
    [ProducesResponseType(typeof(CheckInResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CheckInResponse>> CheckIn(
        CheckInRequest request, [FromServices] TrainingService training, CancellationToken ct)
    {
        var today = _clock.Today;
        var now = _clock.UtcNow;

        var member = request.MemberId is { } id
            ? await _db.Members.Include(m => m.HomeBranch).FirstOrDefaultAsync(m => m.Id == id, ct)
            : string.IsNullOrWhiteSpace(request.QrToken)
                ? null
                : await _db.Members.Include(m => m.HomeBranch)
                    .FirstOrDefaultAsync(m => m.QrToken == request.QrToken.Trim(), ct);

        if (member is null)
            return Ok(new CheckInResponse
            {
                Admitted = false,
                Headline = "Card not recognised",
                Message = "That QR code does not match a member. Search by name or number instead.",
                Warnings = Array.Empty<string>(),
                BlockReason = "Unknown member"
            });

        var subscription = await _db.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.MemberId == member.Id)
            .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Frozen)
            .OrderByDescending(s => s.EndsOn)
            .FirstOrDefaultAsync(ct);

        var dues = await _db.Invoices
            .Where(i => i.MemberId == member.Id && i.AmountDue > 0)
            .Where(i => i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Refunded)
            .SumAsync(i => (decimal?)i.AmountDue, ct) ?? 0m;

        var warnings = new List<string>();
        string? block = null;

        if (subscription is null)
            block = "No active membership on file.";
        else if (subscription.Status == SubscriptionStatus.Frozen)
            block = $"Membership is frozen until {subscription.FreezeEndsOn:dd MMM yyyy}.";
        else if (subscription.EndsOn < today)
            block = $"Membership expired on {subscription.EndsOn:dd MMM yyyy}.";
        else if (subscription.Plan.AccessScope == AccessScope.HomeBranch && member.HomeBranchId != request.BranchId)
            block = $"This plan covers {member.HomeBranch.Name} only.";
        else if (subscription.Plan.AccessWindowStart is { } from && subscription.Plan.AccessWindowEnd is { } to)
        {
            // Off-peak tiers only open the doors inside their window.
            var nowIst = TimeOnly.FromDateTime(_clock.LocalNow);
            if (nowIst < from || nowIst > to)
                block = $"{subscription.Plan.Name} is valid {from:HH\\:mm}–{to:HH\\:mm}.";
        }

        if (subscription is not null && subscription.EndsOn >= today)
        {
            var daysLeft = subscription.EndsOn.DayNumber - today.DayNumber;
            if (daysLeft <= 7) warnings.Add($"Membership ends in {daysLeft} day{(daysLeft == 1 ? "" : "s")}.");
        }
        // Dues are a conversation at the desk, not a locked door.
        if (dues > 0) warnings.Add($"₹{dues:N0} outstanding.");
        if (!member.WaiverSigned) warnings.Add("Waiver not signed.");

        var alreadyIn = await _db.CheckIns
            .FirstOrDefaultAsync(c => c.MemberId == member.Id && c.CheckOutAtUtc == null && !c.WasBlocked, ct);
        if (alreadyIn is not null)
            warnings.Add($"Already checked in at {alreadyIn.CheckInAtUtc.AddMinutes(330):HH\\:mm} — not counted twice.");

        var admitted = block is null || request.Override;

        // A refused entry is still recorded: the pattern of who was turned away and why is the
        // most useful attendance data the owner has, and it is invisible if only successes log.
        CheckIn? row = null;
        if (alreadyIn is null)
        {
            row = new CheckIn
            {
                MemberId = member.Id,
                BranchId = request.BranchId,
                ClassSessionId = request.ClassSessionId,
                CheckInAtUtc = now,
                VisitDate = today,
                Source = request.Source,
                DeviceId = request.DeviceId,
                WasBlocked = !admitted,
                BlockReason = admitted ? null : block,
                RecordedBy = User.Identity?.Name
            };
            _db.CheckIns.Add(row);
        }

        if (admitted)
        {
            if (alreadyIn is null)
            {
                // Streaks: consecutive visit days. A same-day repeat leaves the streak alone.
                if (member.LastVisitOn is { } last && last == today.AddDays(-1)) member.CurrentStreakDays += 1;
                else if (member.LastVisitOn != today) member.CurrentStreakDays = 1;

                member.LongestStreakDays = Math.Max(member.LongestStreakDays, member.CurrentStreakDays);
                member.LastVisitOn = today;
            }

            // Class attendance is marked even when the visit is already open: a member who
            // came in for the floor at six and scans again for the seven o'clock class is
            // one visit and one class, and skipping the second scan loses the roster mark.
            if (request.ClassSessionId is { } sessionId)
            {
                var booking = await _db.Bookings.FirstOrDefaultAsync(
                    b => b.ClassSessionId == sessionId && b.MemberId == member.Id
                      && b.Status == BookingStatus.Booked, ct);
                if (booking is not null)
                {
                    booking.Status = BookingStatus.Attended;
                    booking.CheckedInAtUtc = now;
                    var session = await _db.ClassSessions.FirstAsync(s => s.Id == sessionId, ct);
                    session.AttendedCount++;
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        // A refused scan does not change the head-count, so it does not move the meter.
        if (admitted && alreadyIn is null)
        {
            await PushOccupancyAsync(request.BranchId, ct);
            // Streak badges and the milestone post ride on the visit that earned them.
            await training.RecordStreakMilestoneAsync(member.Id, ct);
        }

        // Queried from the session side so the Includes survive — an Include before a Select
        // that reshapes the query is silently dropped, and the mapper would then hit nulls.
        var todaysClasses = await _db.ClassSessions.AsNoTracking()
            .Include(s => s.ClassFormat).Include(s => s.Branch)
            .Include(s => s.Trainer).Include(s => s.Room)
            .Where(s => s.SessionDate == today)
            .Where(s => s.Bookings.Any(b => b.MemberId == member.Id
                && (b.Status == BookingStatus.Booked || b.Status == BookingStatus.Attended)))
            .OrderBy(s => s.StartTime)
            .ToListAsync(ct);

        _log.LogInformation("Check-in {Result} for {Code} at branch {BranchId}",
            admitted ? "admitted" : "refused", member.MemberCode, request.BranchId);

        return Ok(new CheckInResponse
        {
            Admitted = admitted,
            Headline = admitted
                ? $"Welcome back, {member.FullName.Split(' ')[0]}"
                : "Entry needs the desk",
            Message = admitted
                ? member.CurrentStreakDays > 1
                    ? $"{member.CurrentStreakDays}-day streak. Have a good session."
                    : "Have a good session."
                : block ?? "Membership check failed.",
            CheckInId = row?.Id,
            MemberId = member.Id,
            MemberCode = member.MemberCode,
            FullName = member.FullName,
            PhotoUrl = member.PhotoUrl,
            PlanName = subscription?.Plan.Name,
            MembershipEndsOn = subscription?.EndsOn.ToString("yyyy-MM-dd"),
            DaysLeft = subscription is null ? null : subscription.EndsOn.DayNumber - today.DayNumber,
            DuesOutstanding = dues,
            CurrentStreakDays = member.CurrentStreakDays,
            Warnings = warnings,
            TodaysClasses = todaysClasses.Select(s => new AdminSessionRow
            {
                Id = s.Id,
                Date = s.SessionDate.ToString("yyyy-MM-dd"),
                StartTime = s.StartTime.ToString("HH\\:mm"),
                EndTime = s.StartTime.AddMinutes(s.DurationMinutes).ToString("HH\\:mm"),
                FormatName = s.ClassFormat.Name,
                BranchName = s.Branch.Name,
                BranchId = s.BranchId,
                TrainerName = s.Trainer.FullName,
                IsSubstitute = s.SubstituteTrainerId != null,
                RoomName = s.Room?.Name,
                Capacity = s.Capacity,
                BookedCount = s.BookedCount,
                WaitlistCount = s.WaitlistCount,
                AttendedCount = s.AttendedCount,
                Status = s.Status,
                CancellationReason = s.CancellationReason,
                FillPercent = s.Capacity == 0 ? 0 : (int)Math.Round(s.BookedCount * 100d / s.Capacity)
            }).ToList(),
            BlockReason = admitted ? null : block
        });
    }

    [HttpPost("checkout/{id:int}")]
    public async Task<IActionResult> CheckOut(int id, CancellationToken ct)
    {
        var row = await _db.CheckIns.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (row is null) return NotFound();
        if (row.CheckOutAtUtc is not null) return Ok(new { alreadyClosed = true });

        row.CheckOutAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        await PushOccupancyAsync(row.BranchId, ct);
        return Ok(new { minutes = (int)(row.CheckOutAtUtc.Value - row.CheckInAtUtc).TotalMinutes });
    }

    /// <summary>Closes every open visit at a branch — the button the desk presses at lock-up.</summary>
    [HttpPost("checkout-all")]
    public async Task<IActionResult> CheckOutAll([FromQuery] int branchId, CancellationToken ct)
    {
        var open = await _db.CheckIns
            .Where(c => c.BranchId == branchId && c.CheckOutAtUtc == null && !c.WasBlocked)
            .ToListAsync(ct);

        foreach (var row in open) row.CheckOutAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        await PushOccupancyAsync(branchId, ct);
        return Ok(new { closed = open.Count });
    }

    [HttpGet("today")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Today([FromQuery] int? branchId, [FromQuery] DateOnly? date, CancellationToken ct)
    {
        var day = date ?? _clock.Today;

        var rows = await _db.CheckIns.AsNoTracking()
            .Where(c => c.VisitDate == day && (branchId == null || c.BranchId == branchId))
            .OrderByDescending(c => c.CheckInAtUtc)
            .Select(c => new AttendanceRow
            {
                Id = c.Id,
                MemberId = c.MemberId,
                MemberCode = c.Member.MemberCode,
                FullName = c.Member.FullName,
                PhotoUrl = c.Member.PhotoUrl,
                BranchName = c.Branch.Name,
                CheckInAtUtc = c.CheckInAtUtc,
                CheckOutAtUtc = c.CheckOutAtUtc,
                DurationMinutes = null,
                Source = c.Source,
                WasBlocked = c.WasBlocked,
                BlockReason = c.BlockReason,
                ClassName = c.ClassSession != null ? c.ClassSession.ClassFormat.Name : null
            })
            .ToListAsync(ct);

        var enriched = rows.Select(r => r with
        {
            DurationMinutes = r.CheckOutAtUtc is null
                ? null
                : (int)(r.CheckOutAtUtc.Value - r.CheckInAtUtc).TotalMinutes
        }).ToList();

        return Ok(new
        {
            date = day.ToString("yyyy-MM-dd"),
            total = enriched.Count(r => !r.WasBlocked),
            onFloor = enriched.Count(r => !r.WasBlocked && r.CheckOutAtUtc is null),
            refused = enriched.Count(r => r.WasBlocked),
            rows = enriched
        });
    }

    /// <summary>
    /// Visits bucketed by weekday and hour over the window. This is the chart that tells the
    /// owner which slots to add classes to and which to staff down.
    /// </summary>
    [HttpGet("heatmap")]
    [ProducesResponseType(typeof(HeatmapResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<HeatmapResponse>> Heatmap(
        [FromQuery] int? branchId, [FromQuery] int days = 28, CancellationToken ct = default)
    {
        var span = Math.Clamp(days, 7, 180);
        var since = _clock.Today.AddDays(-span);

        var visits = await _db.CheckIns.AsNoTracking()
            .Where(c => c.VisitDate >= since && !c.WasBlocked && (branchId == null || c.BranchId == branchId))
            .Select(c => new { c.CheckInAtUtc, c.VisitDate })
            .ToListAsync(ct);

        // Buckets are IST wall-clock hours; the stored instants are UTC.
        var cells = visits
            .Select(v => v.CheckInAtUtc.AddMinutes(330))
            .GroupBy(ist => (Day: (int)ist.DayOfWeek, ist.Hour))
            .Select(g => new HeatmapCell { DayOfWeek = g.Key.Day, Hour = g.Key.Hour, Count = g.Count() })
            .OrderBy(c => c.DayOfWeek).ThenBy(c => c.Hour)
            .ToList();

        var peak = cells.OrderByDescending(c => c.Count).FirstOrDefault();
        var dayNames = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

        var daily = visits
            .GroupBy(v => v.VisitDate)
            .OrderBy(g => g.Key)
            .Select(g => new TimeSeriesPoint
            {
                Label = g.Key.ToString("dd MMM"),
                Date = g.Key.ToString("yyyy-MM-dd"),
                Value = g.Count()
            })
            .ToList();

        return Ok(new HeatmapResponse
        {
            Cells = cells,
            PeakCount = peak?.Count ?? 0,
            PeakLabel = peak is null ? "No visits yet" : $"{dayNames[peak.DayOfWeek]} {peak.Hour:00}:00",
            TotalVisits = visits.Count,
            DaysCovered = span,
            Daily = daily
        });
    }

    [HttpGet("absentees")]
    [ProducesResponseType(typeof(IReadOnlyList<AbsenteeRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AbsenteeRow>>> Absentees(
        [FromQuery] int? branchId, [FromQuery] int days = OperationsWorker.AbsentDaysThreshold,
        CancellationToken ct = default)
    {
        var today = _clock.Today;
        var threshold = today.AddDays(-Math.Clamp(days, 3, 180));

        var rows = await _db.Members.AsNoTracking()
            .Where(m => m.Status == MemberStatus.Active && (branchId == null || m.HomeBranchId == branchId))
            .Where(m => m.LastVisitOn == null || m.LastVisitOn < threshold)
            .OrderBy(m => m.LastVisitOn ?? DateOnly.MinValue)
            .Take(200)
            .Select(m => new
            {
                m.Id, m.MemberCode, m.FullName, m.Phone, BranchName = m.HomeBranch.Name, m.LastVisitOn,
                Plan = _db.Subscriptions
                    .Where(s => s.MemberId == m.Id && s.Status == SubscriptionStatus.Active)
                    .OrderByDescending(s => s.EndsOn)
                    .Select(s => new { s.Plan.Name, s.EndsOn })
                    .FirstOrDefault(),
                WinBack = _db.Notifications
                    .Where(n => n.MemberId == m.Id && n.Kind == NotificationKind.WinBack)
                    .OrderByDescending(n => n.CreatedAtUtc)
                    .Select(n => (DateTime?)n.CreatedAtUtc)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return Ok(rows.Select(m => new AbsenteeRow
        {
            MemberId = m.Id,
            MemberCode = m.MemberCode,
            FullName = m.FullName,
            Phone = m.Phone,
            BranchName = m.BranchName,
            LastVisitOn = m.LastVisitOn?.ToString("yyyy-MM-dd"),
            DaysSinceVisit = m.LastVisitOn is null ? 999 : today.DayNumber - m.LastVisitOn.Value.DayNumber,
            PlanName = m.Plan?.Name,
            MembershipEndsOn = m.Plan?.EndsOn.ToString("yyyy-MM-dd"),
            WinBackSent = m.WinBack is not null,
            WinBackSentAtUtc = m.WinBack
        }).ToList());
    }

    /// <summary>
    /// Fires the win-back for one member or a whole selection, straight from the absentee
    /// list. It runs the same <see cref="ChurnService"/> sequence the churn radar uses — one
    /// implementation, so the fortnight cool-off and the desk call-back task apply wherever
    /// the button is pressed. No discount unless the desk asks for one: the absentee nudge is
    /// a message, and money is the radar's lever, not this one's.
    /// </summary>
    [HttpPost("winback")]
    public async Task<IActionResult> WinBack(
        [FromBody] AbsenteeWinBackRequest request, [FromServices] ChurnService churn, CancellationToken ct)
    {
        if (request.MemberIds.Length == 0) return BadRequest(new ProblemDetails { Title = "Pick at least one member." });

        var options = new WinBackOptions
        {
            DiscountPercent = Math.Clamp(request.DiscountPercent ?? 0m, 0m, 60m),
            OfferValidDays = 14,
            Message = request.Message,
            SendWhatsApp = true,
            SendEmail = false,
            Force = request.Force
        };

        var actor = User.Identity?.Name ?? "admin";
        var sent = 0;
        var skipped = new List<object>();

        foreach (var memberId in request.MemberIds.Distinct())
        {
            var result = await churn.RunWinBackAsync(memberId, options, actor, ct);
            if (result.Sent) sent++;
            else skipped.Add(new { memberId, reason = result.Message });
        }

        if (sent == 0 && skipped.Count == 0) return NotFound();
        return Ok(new { members = sent, skipped = skipped.Count, details = skipped });
    }

    /// <summary>Runs the housekeeping sweep now — used by the "refresh operations" action.</summary>
    [HttpPost("run-sweep")]
    public async Task<IActionResult> RunSweep(
        [FromServices] IServiceScopeFactory scopes, CancellationToken ct)
    {
        var result = await OperationsWorker.RunOnceAsync(scopes, ct);
        return Ok(result);
    }
}

public record AbsenteeWinBackRequest
{
    public int[] MemberIds { get; init; } = Array.Empty<int>();
    /// <summary>Blank uses the default copy, which names the member and their branch.</summary>
    public string? Message { get; init; }
    /// <summary>Optional — attaches a personal coupon through the same offer engine the radar uses.</summary>
    public decimal? DiscountPercent { get; init; }
    /// <summary>Overrides the fortnight cool-off between win-backs.</summary>
    public bool Force { get; init; }
}
