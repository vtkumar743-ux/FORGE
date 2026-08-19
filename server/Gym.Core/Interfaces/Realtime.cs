namespace Gym.Core.Interfaces;

/// <summary>
/// The occupancy figures the meter renders — one branch, one moment. Lives in Core so the
/// background sweep, the kiosk and the SignalR hub all agree on the shape.
/// </summary>
public record BranchOccupancySnapshot
{
    public required int BranchId { get; init; }
    public required string BranchName { get; init; }
    public required string BranchSlug { get; init; }
    public required int CurrentCount { get; init; }
    public required int Capacity { get; init; }
    public required int PercentFull { get; init; }
    /// <summary>0 Comfortable · 1 Busy · 2 Peak — matches <c>OccupancyBand</c>.</summary>
    public required int Band { get; init; }
    public required DateTime AsOfUtc { get; init; }
}

/// <summary>
/// Push seam for the live meter. Implemented over SignalR in the API; the interface lives
/// here so services that change occupancy (check-in, check-out, the operations sweep) can
/// announce it without the Infrastructure project taking a dependency on the hub.
/// </summary>
public interface IRealtimeNotifier
{
    Task OccupancyChangedAsync(BranchOccupancySnapshot snapshot, CancellationToken ct = default);
}

/// <summary>No-op used by design-time tooling and tests; the API always replaces it.</summary>
public sealed class NullRealtimeNotifier : IRealtimeNotifier
{
    public Task OccupancyChangedAsync(BranchOccupancySnapshot snapshot, CancellationToken ct = default) => Task.CompletedTask;
}
