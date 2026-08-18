using Gym.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Gym.Api.Hubs;

/// <summary>
/// Contract for the live occupancy meter (Module 4.1) and waitlist promotions. Established in
/// Phase 0 so the public site, member portal and admin dashboard can all be built against a
/// stable client-method surface; the broadcasting side lands with the check-in pipeline.
/// </summary>
[AllowAnonymous]
public class OccupancyHub : Hub<IOccupancyClient>
{
    public const string Route = "/hubs/occupancy";

    private static string GroupFor(string branchSlug) => $"branch:{branchSlug}";

    /// <summary>Public pages subscribe per branch so one busy branch does not fan out to everyone.</summary>
    public Task SubscribeToBranch(string branchSlug) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(branchSlug));

    public Task UnsubscribeFromBranch(string branchSlug) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(branchSlug));

    /// <summary>Admin dashboards watch the whole network in one subscription.</summary>
    [Authorize(Roles = "Admin")]
    public Task SubscribeToNetwork() =>
        Groups.AddToGroupAsync(Context.ConnectionId, "network");
}

/// <summary>Strongly-typed client surface — the TypeScript client mirrors these names.</summary>
public interface IOccupancyClient
{
    Task OccupancyChanged(BranchOccupancyResponse occupancy);
    Task SessionCapacityChanged(SessionCapacityUpdate update);
    Task WaitlistPromoted(WaitlistPromotion promotion);
}

public record SessionCapacityUpdate
{
    public required int ClassSessionId { get; init; }
    public required int BookedCount { get; init; }
    public required int Capacity { get; init; }
    public required int WaitlistCount { get; init; }
    public required int SpotsLeft { get; init; }
}

public record WaitlistPromotion
{
    public required int BookingId { get; init; }
    public required int ClassSessionId { get; init; }
    public required int MemberId { get; init; }
    public required string ClassName { get; init; }
    public required DateTime StartsAtUtc { get; init; }
}
