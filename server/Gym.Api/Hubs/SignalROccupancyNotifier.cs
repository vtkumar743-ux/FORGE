using Gym.Api.Controllers;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Gym.Api.Hubs;

/// <summary>
/// The push half of the live occupancy meter (Module 4.1). Fans a branch's new reading out
/// to that branch's subscribers and to the network group the admin dashboard watches — two
/// groups rather than one broadcast, so a busy Whitefield evening does not wake every
/// browser that happens to have the Koramangala page open.
/// </summary>
public class SignalROccupancyNotifier : IRealtimeNotifier
{
    private readonly IHubContext<OccupancyHub, IOccupancyClient> _hub;
    private readonly ILogger<SignalROccupancyNotifier> _log;

    public SignalROccupancyNotifier(
        IHubContext<OccupancyHub, IOccupancyClient> hub, ILogger<SignalROccupancyNotifier> log)
    {
        _hub = hub;
        _log = log;
    }

    public async Task OccupancyChangedAsync(BranchOccupancySnapshot snapshot, CancellationToken ct = default)
    {
        var payload = new BranchOccupancyResponse
        {
            BranchId = snapshot.BranchId,
            BranchName = snapshot.BranchName,
            BranchSlug = snapshot.BranchSlug,
            CurrentCount = snapshot.CurrentCount,
            Capacity = snapshot.Capacity,
            PercentFull = snapshot.PercentFull,
            Band = (OccupancyBand)snapshot.Band,
            AsOfUtc = snapshot.AsOfUtc
        };

        try
        {
            await _hub.Clients.Group($"branch:{snapshot.BranchSlug}").OccupancyChanged(payload);
            await _hub.Clients.Group("network").OccupancyChanged(payload);
        }
        catch (Exception ex)
        {
            // A check-in must succeed whether or not anyone is listening: the desk admitting
            // a member cannot fail because a websocket is having a bad minute.
            _log.LogWarning(ex, "Occupancy push failed for branch {Branch}", snapshot.BranchSlug);
        }
    }
}
