using Gym.Core.Interfaces;

namespace Gym.Infrastructure.Services;

/// <summary>
/// Everything in this product is Asia/Kolkata. India has no daylight saving, so a fixed
/// +05:30 offset is exact — no timezone database lookup and no ambiguity around midnight.
/// </summary>
public class SystemClock : IClock
{
    public static readonly TimeSpan IndiaOffset = TimeSpan.FromMinutes(330);

    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime LocalNow => DateTime.UtcNow.Add(IndiaOffset);
    public DateOnly Today => DateOnly.FromDateTime(LocalNow);
}
