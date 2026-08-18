using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.Services;

public class MessagingOptions
{
    public const string SectionName = "Whatsapp";
    /// <summary>"none" until the client connects a WhatsApp Business or DLT SMS provider.</summary>
    public string Provider { get; set; } = "none";
    public string ApiKey { get; set; } = string.Empty;
    public bool IsConfigured => !string.Equals(Provider, "none", StringComparison.OrdinalIgnoreCase)
                                && !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>
/// Writes one Notification row per channel. The in-app row always lands, so a dunning
/// reminder or win-back is visible in the product even before a WhatsApp provider is wired
/// up; external channels are queued with <see cref="Notification.SentAtUtc"/> left null and
/// a delivery error explaining why, which is what the comms screen shows the owner.
/// </summary>
public class NotificationDispatcher : INotificationDispatcher
{
    private readonly GymDbContext _db;
    private readonly MessagingOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<NotificationDispatcher> _log;

    public NotificationDispatcher(
        GymDbContext db, MessagingOptions options, IClock clock, ILogger<NotificationDispatcher> log)
    {
        _db = db;
        _options = options;
        _clock = clock;
        _log = log;
    }

    public Task<int> SendAsync(OutboundMessage message, CancellationToken ct = default) =>
        SendManyAsync(new[] { message }, ct);

    public async Task<int> SendManyAsync(IEnumerable<OutboundMessage> messages, CancellationToken ct = default)
    {
        var rows = new List<Notification>();

        foreach (var message in messages)
        {
            var channels = message.Channels.Count == 0
                ? new[] { NotificationChannel.InApp }
                : message.Channels.Distinct().ToArray();

            foreach (var channel in channels)
            {
                var external = channel != NotificationChannel.InApp;
                rows.Add(new Notification
                {
                    MemberId = message.MemberId,
                    UserId = message.UserId,
                    Kind = message.Kind,
                    Channel = channel,
                    Title = message.Title,
                    Body = message.Body,
                    ActionUrl = message.ActionUrl,
                    TemplateKey = message.TemplateKey,
                    PayloadJson = message.PayloadJson,
                    SentAtUtc = external && !_options.IsConfigured ? null : _clock.UtcNow,
                    DeliveryError = external && !_options.IsConfigured
                        ? $"Queued — no {channel} provider is connected (Whatsapp:Provider is \"{_options.Provider}\")."
                        : null
                });
            }
        }

        if (rows.Count == 0) return 0;

        _db.Notifications.AddRange(rows);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Dispatched {Count} notification row(s) across {Channels}",
            rows.Count, string.Join(", ", rows.Select(r => r.Channel).Distinct()));

        return rows.Count;
    }
}
