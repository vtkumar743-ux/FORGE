using Gym.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.Media;

/// <summary>
/// The same local storage, rooted outside <c>wwwroot</c>.
///
/// Progress photos are body photographs. Everything under wwwroot is served statically to
/// anyone holding the URL, and "the URL is long" is not an access-control model for that kind
/// of picture. These files live where no static-file middleware is mapped, and the portal
/// streams them through an endpoint that checks who is asking.
/// </summary>
public class PrivateMediaStorage : LocalMediaStorage
{
    public PrivateMediaStorage(MediaStorageOptions options, ILogger<LocalMediaStorage> log)
        : base(options, log)
    {
        Root = options.RootPath;
        BasePath = options.PublicBasePath;
    }

    /// <summary>Physical root, so a caller can open the file it stored.</summary>
    public string Root { get; }

    /// <summary>Prefix the stored URLs carry — never a route this application maps statically.</summary>
    public string BasePath { get; }

    /// <summary>
    /// Turns a stored URL back into a path inside the root, refusing anything that escapes it.
    /// Returns null when the URL does not belong to this store.
    /// </summary>
    public string? ResolvePath(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith(BasePath, StringComparison.OrdinalIgnoreCase))
            return null;

        var relative = url[BasePath.Length..].TrimStart('/');
        var absolute = Path.GetFullPath(Path.Combine(Root, relative));
        var rootFull = Path.GetFullPath(Root);

        return absolute.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) ? absolute : null;
    }
}
