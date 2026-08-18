namespace Gym.Core.Interfaces;

/// <summary>Injected everywhere instead of DateTime.UtcNow so schedules and dunning are testable.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
    /// <summary>Branch-local time — the whole product operates on IST.</summary>
    DateTime LocalNow { get; }
    DateOnly Today { get; }
}

public record StoredMedia(
    string OriginalUrl,
    string ContentType,
    long SizeBytes,
    int? Width,
    int? Height,
    IReadOnlyDictionary<string, string> Variants,
    string? BlurDataUrl);

/// <summary>
/// Local disk in v1 (wwwroot/media); the interface is the seam that lets S3/Azure Blob
/// drop in later without touching callers.
/// </summary>
public interface IMediaStorage
{
    Task<StoredMedia> SaveImageAsync(Stream content, string fileName, string? folder = null, CancellationToken ct = default);
    Task<StoredMedia> SaveFileAsync(Stream content, string fileName, string contentType, string? folder = null, CancellationToken ct = default);
    Task DeleteAsync(string url, CancellationToken ct = default);
}

public record TokenPair(string AccessToken, DateTime AccessTokenExpiresAtUtc, string RefreshToken, DateTime RefreshTokenExpiresAtUtc);
