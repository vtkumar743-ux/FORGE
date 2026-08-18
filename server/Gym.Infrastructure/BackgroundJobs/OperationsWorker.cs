using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.BackgroundJobs;

public record OperationsSweep(int SessionsClosed, int NoShows, int SessionsCreated, int Expired, int AbsenteeAlerts);

/// <summary>
/// The housekeeping the floor depends on: keep four weeks of sessions materialised ahead of
/// today, close finished classes and mark no-shows, expire memberships that ran out, and
/// raise the absentee alert that drives the win-back message ("no visit in 10 days").
/// </summary>
public class OperationsWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(2);
    /// <summary>The spec's absentee threshold.</summary>
    public const int AbsentDaysThreshold = 10;

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<OperationsWorker> _log;

    public OperationsWorker(IServiceScopeFactory scopes, ILogger<OperationsWorker> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await RunOnceAsync(_scopes, stoppingToken);
                _log.LogInformation(
                    "Operations sweep — {Closed} session(s) closed, {NoShows} no-show(s), " +
                    "{Created} session(s) materialised, {Expired} membership(s) expired, {Alerts} absentee alert(s)",
                    result.SessionsClosed, result.NoShows, result.SessionsCreated, result.Expired, result.AbsenteeAlerts);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Operations sweep failed; retrying next cycle");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    public static async Task<OperationsSweep> RunOnceAsync(IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GymDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var scheduling = scope.ServiceProvider.GetRequiredService<SchedulingService>();
        var notifier = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

        var today = clock.Today;

        var (closed, noShows) = await scheduling.CloseFinishedSessionsAsync(ct);

        // Keep a four-week booking horizon materialised so the timetable is never empty.
        var horizon = today.AddDays(28);
        var schedules = await db.ClassSchedules
            .Where(s => s.IsActive && s.EffectiveFrom <= horizon && (s.EffectiveTo == null || s.EffectiveTo >= today))
            .ToListAsync(ct);

        var created = 0;
        foreach (var schedule in schedules)
            created += await scheduling.MaterialiseAsync(schedule, today, horizon, ct);

        var expired = await ExpireAsync(db, today, ct);
        var alerts = await AbsenteeAlertsAsync(db, notifier, clock, ct);

        return new OperationsSweep(closed, noShows, created, expired, alerts);
    }

    /// <summary>Runs out memberships whose end date has passed and demotes the member with them.</summary>
    private static async Task<int> ExpireAsync(GymDbContext db, DateOnly today, CancellationToken ct)
    {
        var lapsed = await db.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active && s.EndsOn < today)
            .Take(500)
            .ToListAsync(ct);

        if (lapsed.Count == 0) return 0;

        foreach (var subscription in lapsed) subscription.Status = SubscriptionStatus.Expired;

        var memberIds = lapsed.Select(s => s.MemberId).Distinct().ToList();
        var stillCovered = await db.Subscriptions
            .Where(s => memberIds.Contains(s.MemberId) && s.Status == SubscriptionStatus.Active)
            .Select(s => s.MemberId)
            .Distinct()
            .ToListAsync(ct);

        var toExpire = memberIds.Except(stillCovered).ToList();
        var members = await db.Members.Where(m => toExpire.Contains(m.Id)).ToListAsync(ct);
        foreach (var member in members.Where(m => m.Status == MemberStatus.Active))
            member.Status = MemberStatus.Expired;

        await db.SaveChangesAsync(ct);
        return lapsed.Count;
    }

    /// <summary>
    /// "No visit in 10 days" on an active membership. One alert per member per fortnight —
    /// chasing the same person daily is how a win-back message becomes spam.
    /// </summary>
    private static async Task<int> AbsenteeAlertsAsync(
        GymDbContext db, INotificationDispatcher notifier, IClock clock, CancellationToken ct)
    {
        var today = clock.Today;
        var cutoff = today.AddDays(-AbsentDaysThreshold);
        var quietSince = clock.UtcNow.AddDays(-14);

        var candidates = await db.Members
            .AsNoTracking()
            .Where(m => m.Status == MemberStatus.Active)
            .Where(m => m.LastVisitOn == null || m.LastVisitOn < cutoff)
            .Where(m => !db.Notifications.Any(n =>
                n.MemberId == m.Id && n.Kind == NotificationKind.WinBack && n.CreatedAtUtc >= quietSince))
            .OrderBy(m => m.LastVisitOn)
            .Take(120)
            .Select(m => new { m.Id, m.FullName, m.LastVisitOn })
            .ToListAsync(ct);

        if (candidates.Count == 0) return 0;

        var messages = candidates.Select(m => new OutboundMessage
        {
            MemberId = m.Id,
            Kind = NotificationKind.WinBack,
            Title = "We have not seen you in a while",
            Body = m.LastVisitOn is null
                ? $"{FirstName(m.FullName)}, your membership is live but you have not checked in yet. " +
                  "Book a class and a coach will meet you on the floor."
                : $"{FirstName(m.FullName)}, your last visit was {m.LastVisitOn:dd MMM}. " +
                  "Pick a class this week and we will hold you a spot.",
            ActionUrl = "/portal/booking",
            TemplateKey = "winback.absentee",
            Channels = new[] { NotificationChannel.InApp, NotificationChannel.WhatsApp }
        }).ToList();

        await notifier.SendManyAsync(messages, ct);
        return messages.Count;
    }

    private static string FirstName(string fullName) =>
        fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? fullName;
}
