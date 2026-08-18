using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.BackgroundJobs;

/// <summary>
/// The collections ladder: a reminder at D-7, D-3, on the due date, and then weekly while an
/// invoice stays overdue. State lives on the invoice (<c>RemindersSent</c>,
/// <c>LastReminderAtUtc</c>) rather than in the worker, so a restart never double-chases a
/// member and never silently drops one either.
/// </summary>
public class DunningWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DunningWorker> _log;

    public DunningWorker(IServiceScopeFactory scopes, ILogger<DunningWorker> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let migrations and the seeder finish before the first sweep touches the tables.
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var sent = await RunOnceAsync(_scopes, stoppingToken);
                if (sent > 0) _log.LogInformation("Dunning sweep sent {Count} reminder(s)", sent);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Dunning sweep failed; retrying next cycle");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Exposed so the admin panel's "run collections now" button reuses one code path.</summary>
    public static async Task<int> RunOnceAsync(IServiceScopeFactory scopes, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GymDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var notifier = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

        var today = clock.Today;
        var now = clock.UtcNow;

        var open = await db.Invoices
            .Include(i => i.Member)
            .Where(i => i.AmountDue > 0
                     && (i.Status == InvoiceStatus.Issued
                      || i.Status == InvoiceStatus.PartiallyPaid
                      || i.Status == InvoiceStatus.Overdue))
            .Where(i => i.DueOn <= today.AddDays(7))
            .OrderBy(i => i.DueOn)
            .Take(400)
            .ToListAsync(ct);

        var messages = new List<OutboundMessage>();

        foreach (var invoice in open)
        {
            // An invoice past its due date is Overdue whether or not anyone has chased it.
            if (invoice.DueOn < today && invoice.Status != InvoiceStatus.Overdue)
                invoice.Status = InvoiceStatus.Overdue;

            var daysToDue = invoice.DueOn.DayNumber - today.DayNumber;
            var rung = Rung(daysToDue);
            if (rung is null) continue;

            // One rung per invoice per day, and overdue chasing settles to weekly.
            if (invoice.LastReminderAtUtc is { } last)
            {
                var quietDays = daysToDue < 0 ? 7 : 1;
                if ((now - last).TotalDays < quietDays) continue;
            }
            if (invoice.RemindersSent >= RungIndex(daysToDue) && daysToDue >= 0) continue;

            invoice.RemindersSent += 1;
            invoice.LastReminderAtUtc = now;

            messages.Add(new OutboundMessage
            {
                MemberId = invoice.MemberId,
                Kind = NotificationKind.PaymentDue,
                Title = rung,
                Body = $"Invoice {invoice.InvoiceNumber} has ₹{invoice.AmountDue:N0} outstanding, " +
                       $"due {invoice.DueOn:dd MMM yyyy}. Pay by UPI, card or at the desk.",
                ActionUrl = $"/portal/billing/{invoice.Id}",
                TemplateKey = daysToDue switch
                {
                    >= 7 => "dunning.d7",
                    >= 3 => "dunning.d3",
                    >= 0 => "dunning.dday",
                    _ => "dunning.overdue"
                },
                // Money reminders go out on every channel the member has consented to.
                Channels = new[]
                {
                    NotificationChannel.InApp, NotificationChannel.WhatsApp,
                    NotificationChannel.Sms, NotificationChannel.Email
                }
            });
        }

        if (messages.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            await notifier.SendManyAsync(messages, ct);
        }
        else if (open.Any(i => i.Status == InvoiceStatus.Overdue))
        {
            await db.SaveChangesAsync(ct);
        }

        return messages.Count;
    }

    private static string? Rung(int daysToDue) => daysToDue switch
    {
        7 => "Your membership renews in a week",
        3 => "Renewal due in 3 days",
        0 => "Renewal due today",
        < 0 => "Payment overdue",
        _ => null
    };

    /// <summary>Ladder position, used to stop the same rung firing twice.</summary>
    private static int RungIndex(int daysToDue) => daysToDue switch
    {
        7 => 1,
        3 => 2,
        0 => 3,
        _ => int.MaxValue
    };
}
