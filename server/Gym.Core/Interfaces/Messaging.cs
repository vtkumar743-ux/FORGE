using Gym.Core.Enums;

namespace Gym.Core.Interfaces;

/// <summary>
/// One outbound message, fanned out across whichever channels the owner has enabled.
/// The in-app row is always written, so nothing the system decided to say is invisible
/// just because a WhatsApp provider has not been connected yet.
/// </summary>
public record OutboundMessage
{
    public int? MemberId { get; init; }
    public int? UserId { get; init; }
    public required NotificationKind Kind { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public string? ActionUrl { get; init; }
    public string? TemplateKey { get; init; }
    /// <summary>Defaults to in-app only; billing and win-back add WhatsApp/email.</summary>
    public IReadOnlyList<NotificationChannel> Channels { get; init; } = new[] { NotificationChannel.InApp };
    public string? PayloadJson { get; init; }
}

public interface INotificationDispatcher
{
    /// <summary>Queues the rows; returns how many channel rows were written.</summary>
    Task<int> SendAsync(OutboundMessage message, CancellationToken ct = default);
    Task<int> SendManyAsync(IEnumerable<OutboundMessage> messages, CancellationToken ct = default);
}
