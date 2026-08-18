using Gym.Api.Contracts;
using Gym.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers;

/// <summary>Public coach profiles (Module 1.4) — the 3:4 portrait cards and their detail pages.</summary>
[ApiController]
[Route("api/trainers")]
[Produces("application/json")]
[AllowAnonymous]
public class TrainersController : ControllerBase
{
    private readonly GymDbContext _db;

    public TrainersController(GymDbContext db) => _db = db;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TrainerResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TrainerResponse>>> All(
        [FromQuery] string? branchSlug, [FromQuery] string? specialty, CancellationToken ct)
    {
        var trainers = await LoadAsync(t => branchSlug == null || t.PrimaryBranch.Slug == branchSlug, ct);

        if (!string.IsNullOrWhiteSpace(specialty))
            trainers = trainers
                .Where(t => t.Specialties.Any(s => s.Contains(specialty, StringComparison.OrdinalIgnoreCase)))
                .ToList();

        return Ok(trainers);
    }

    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(TrainerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainerResponse>> BySlug(string slug, CancellationToken ct)
    {
        var trainer = (await LoadAsync(t => t.Slug == slug, ct)).FirstOrDefault();
        return trainer is null ? NotFound() : Ok(trainer);
    }

    /// <summary>
    /// Specialties and certifications are stored comma-separated; SQL cannot split them, so
    /// the shaping happens after materialising. Class counts come from the live schedule
    /// rather than a stored figure — a coach who stops teaching a format stops advertising it.
    /// </summary>
    private async Task<List<TrainerResponse>> LoadAsync(
        System.Linq.Expressions.Expression<Func<Core.Entities.Trainer, bool>> predicate, CancellationToken ct)
    {
        var rows = await _db.Trainers
            .AsNoTracking()
            .Where(t => t.IsActive && t.ShowOnWebsite)
            .Where(predicate)
            .OrderBy(t => t.DisplayOrder)
            .Select(t => new
            {
                t.Id, t.FullName, t.Slug, t.Headline, t.Bio, t.PortraitUrl, t.DemoVideoUrl,
                t.Specialties, t.Certifications, t.YearsExperience, t.InstagramUrl,
                t.PrimaryBranchId,
                BranchName = t.PrimaryBranch.Name,
                BranchSlug = t.PrimaryBranch.Slug,
                t.PtSessionPrice, t.AcceptsPtClients, t.AverageRating, t.RatingCount, t.DisplayOrder,
                TeachesFormats = t.Schedules.Where(s => s.IsActive).Select(s => s.ClassFormat.Name).Distinct().ToList(),
                WeeklyClassCount = t.Schedules.Count(s => s.IsActive)
            })
            .ToListAsync(ct);

        return rows.Select(t => new TrainerResponse
        {
            Id = t.Id,
            FullName = t.FullName,
            Slug = t.Slug,
            Headline = t.Headline,
            Bio = t.Bio,
            PortraitUrl = t.PortraitUrl ?? $"/media/trainers/{t.Slug}.jpg",
            DemoVideoUrl = t.DemoVideoUrl,
            Specialties = ClassesController.Split(t.Specialties),
            Certifications = ClassesController.Split(t.Certifications),
            YearsExperience = t.YearsExperience,
            InstagramUrl = t.InstagramUrl,
            BranchId = t.PrimaryBranchId,
            BranchName = t.BranchName,
            BranchSlug = t.BranchSlug,
            PtSessionPrice = t.PtSessionPrice,
            AcceptsPtClients = t.AcceptsPtClients,
            AverageRating = t.AverageRating,
            RatingCount = t.RatingCount,
            DisplayOrder = t.DisplayOrder,
            TeachesFormats = t.TeachesFormats,
            WeeklyClassCount = t.WeeklyClassCount
        }).ToList();
    }
}
