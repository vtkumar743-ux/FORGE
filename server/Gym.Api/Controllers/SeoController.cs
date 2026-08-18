using System.Text;
using System.Xml.Linq;
using Gym.Core.Enums;
using Gym.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers;

/// <summary>
/// Sitemap and robots (Module 1.12). Both are generated from the CMS, so a page the owner
/// adds or unpublishes appears or disappears from search without a deploy.
/// </summary>
[ApiController]
[AllowAnonymous]
public class SeoController : ControllerBase
{
    private readonly GymDbContext _db;
    private readonly IConfiguration _config;

    public SeoController(GymDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpGet("/sitemap.xml")]
    [Produces("application/xml")]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        var origin = PublicOrigin();

        var pages = await _db.CmsPages
            .AsNoTracking()
            .Where(p => p.State == PublishState.Published && !p.NoIndex)
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new { p.Slug, Updated = p.UpdatedAtUtc ?? p.PublishedAtUtc ?? p.CreatedAtUtc })
            .ToListAsync(ct);

        var posts = await _db.BlogPosts
            .AsNoTracking()
            .Where(p => p.State == PublishState.Published)
            .Select(p => new { Slug = "journal/" + p.Slug, Updated = p.UpdatedAtUtc ?? p.PublishedAtUtc ?? p.CreatedAtUtc })
            .ToListAsync(ct);

        var trainers = await _db.Trainers
            .AsNoTracking()
            .Where(t => t.IsActive && t.ShowOnWebsite)
            .Select(t => new { Slug = "trainers/" + t.Slug, Updated = t.UpdatedAtUtc ?? t.CreatedAtUtc })
            .ToListAsync(ct);

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(ns + "urlset",
                pages.Concat(posts).Concat(trainers).Select(entry =>
                {
                    // "home" is the site root, not /home — a duplicate URL would split its ranking.
                    var path = entry.Slug == "home" ? string.Empty : entry.Slug;
                    return new XElement(ns + "url",
                        new XElement(ns + "loc", $"{origin}/{path}".TrimEnd('/')),
                        new XElement(ns + "lastmod", entry.Updated.ToString("yyyy-MM-dd")),
                        new XElement(ns + "changefreq", Frequency(entry.Slug)),
                        new XElement(ns + "priority", Priority(entry.Slug)));
                })));

        return Content(doc.Declaration + Environment.NewLine + doc, "application/xml", Encoding.UTF8);
    }

    [HttpGet("/robots.txt")]
    [Produces("text/plain")]
    public IActionResult Robots()
    {
        var origin = PublicOrigin();
        var body = string.Join('\n',
            "User-agent: *",
            "Allow: /",
            // Nothing behind a login has any business in an index.
            "Disallow: /portal",
            "Disallow: /admin",
            "Disallow: /login",
            "Disallow: /register",
            "Disallow: /api/",
            string.Empty,
            $"Sitemap: {origin}/sitemap.xml",
            string.Empty);

        return Content(body, "text/plain", Encoding.UTF8);
    }

    /// <summary>
    /// The public site is served from its own origin in production, which is not necessarily
    /// the API's. Configured explicitly, falling back to the request host in development.
    /// </summary>
    private string PublicOrigin() =>
        (_config["Site:PublicOrigin"] ?? $"{Request.Scheme}://{Request.Host}").TrimEnd('/');

    private static string Frequency(string slug) =>
        slug.StartsWith("journal/") ? "monthly"
        : slug is "home" or "classes" ? "daily"
        : "weekly";

    private static string Priority(string slug) => slug switch
    {
        "home" => "1.0",
        "plans" or "free-trial" => "0.9",
        "classes" or "trainers" or "branches" => "0.8",
        _ when slug.StartsWith("branches/") => "0.8",
        _ when slug.StartsWith("journal/") => "0.5",
        _ => "0.6"
    };
}
