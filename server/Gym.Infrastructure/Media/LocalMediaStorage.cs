using Gym.Core.Interfaces;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Gym.Infrastructure.Media;

public class MediaStorageOptions
{
    public const string SectionName = "Media";
    /// <summary>Physical root; defaults to the API's wwwroot/media.</summary>
    public string RootPath { get; set; } = string.Empty;
    /// <summary>URL prefix the client uses to reach the same files.</summary>
    public string PublicBasePath { get; set; } = "/media";
    /// <summary>WebP renditions generated for every upload (Module 2A media library).</summary>
    public int[] Widths { get; set; } = { 480, 960, 1440, 1920, 2560 };
    public int WebpQuality { get; set; } = 78;
}

/// <summary>
/// v1 stores media on local disk under wwwroot. Every image gets WebP renditions plus a tiny
/// blurred data URI, so the front end never has to render an empty grey box while loading.
/// Swapping to S3/Azure Blob later means one new <see cref="IMediaStorage"/> implementation.
/// </summary>
public class LocalMediaStorage : IMediaStorage
{
    private readonly MediaStorageOptions _options;
    private readonly ILogger<LocalMediaStorage> _log;

    public LocalMediaStorage(MediaStorageOptions options, ILogger<LocalMediaStorage> log)
    {
        _options = options;
        _log = log;
        Directory.CreateDirectory(_options.RootPath);
    }

    public async Task<StoredMedia> SaveImageAsync(Stream content, string fileName, string? folder = null, CancellationToken ct = default)
    {
        var (relativeFolder, absoluteFolder) = ResolveFolder(folder);
        var stem = SafeStem(Path.GetFileNameWithoutExtension(fileName));
        var unique = $"{stem}-{DateTime.UtcNow:yyyyMMddHHmmss}";

        using var image = await Image.LoadAsync(content, ct);
        var originalName = $"{unique}.webp";
        var encoder = new WebpEncoder { Quality = _options.WebpQuality };

        await using (var originalOut = File.Create(Path.Combine(absoluteFolder, originalName)))
            await image.SaveAsync(originalOut, encoder, ct);

        var variants = new Dictionary<string, string>();
        foreach (var width in _options.Widths.Where(w => w < image.Width))
        {
            using var resized = image.Clone(x => x.Resize(new ResizeOptions
            {
                Size = new Size(width, 0),
                Mode = ResizeMode.Max
            }));
            var variantName = $"{unique}-{width}.webp";
            await using var variantOut = File.Create(Path.Combine(absoluteFolder, variantName));
            await resized.SaveAsync(variantOut, encoder, ct);
            variants[width.ToString()] = PublicUrl(relativeFolder, variantName);
        }

        var blur = await BlurDataUrlAsync(image, ct);
        _log.LogInformation("Stored image {File} ({Width}×{Height}) with {Variants} WebP variants",
            originalName, image.Width, image.Height, variants.Count);

        return new StoredMedia(
            PublicUrl(relativeFolder, originalName),
            "image/webp",
            new FileInfo(Path.Combine(absoluteFolder, originalName)).Length,
            image.Width,
            image.Height,
            variants,
            blur);
    }

    public async Task<StoredMedia> SaveFileAsync(Stream content, string fileName, string contentType, string? folder = null, CancellationToken ct = default)
    {
        var (relativeFolder, absoluteFolder) = ResolveFolder(folder);
        var stem = SafeStem(Path.GetFileNameWithoutExtension(fileName));
        var extension = Path.GetExtension(fileName);
        var name = $"{stem}-{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
        var absolutePath = Path.Combine(absoluteFolder, name);

        await using (var output = File.Create(absolutePath))
            await content.CopyToAsync(output, ct);

        return new StoredMedia(
            PublicUrl(relativeFolder, name),
            contentType,
            new FileInfo(absolutePath).Length,
            null, null,
            new Dictionary<string, string>(),
            null);
    }

    public Task DeleteAsync(string url, CancellationToken ct = default)
    {
        if (!url.StartsWith(_options.PublicBasePath, StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        var relative = url[_options.PublicBasePath.Length..].TrimStart('/');
        var absolute = Path.GetFullPath(Path.Combine(_options.RootPath, relative));

        // Refuse anything that escapes the media root, however the URL was constructed.
        if (!absolute.StartsWith(Path.GetFullPath(_options.RootPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to delete outside the media root: {url}");

        if (File.Exists(absolute)) File.Delete(absolute);
        return Task.CompletedTask;
    }

    /// <summary>16px WebP encoded inline — enough to hint the composition, ~400 bytes.</summary>
    private static async Task<string> BlurDataUrlAsync(Image image, CancellationToken ct)
    {
        using var tiny = image.Clone(x => x.Resize(new ResizeOptions { Size = new Size(16, 0), Mode = ResizeMode.Max }));
        using var buffer = new MemoryStream();
        await tiny.SaveAsync(buffer, new WebpEncoder { Quality = 40 }, ct);
        return $"data:image/webp;base64,{Convert.ToBase64String(buffer.ToArray())}";
    }

    private (string Relative, string Absolute) ResolveFolder(string? folder)
    {
        var relative = string.IsNullOrWhiteSpace(folder) ? string.Empty : SafeStem(folder);
        var absolute = Path.Combine(_options.RootPath, relative);
        Directory.CreateDirectory(absolute);
        return (relative, absolute);
    }

    private string PublicUrl(string relativeFolder, string fileName) =>
        string.IsNullOrEmpty(relativeFolder)
            ? $"{_options.PublicBasePath}/{fileName}"
            : $"{_options.PublicBasePath}/{relativeFolder}/{fileName}";

    private static string SafeStem(string value)
    {
        var cleaned = new string(value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '/' ? c : '-').ToArray());
        while (cleaned.Contains("--")) cleaned = cleaned.Replace("--", "-");
        return cleaned.Trim('-', '/').ToLowerInvariant();
    }
}
