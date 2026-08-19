using Gym.Api.Contracts;
using Gym.Core.Entities;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Media;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace Gym.Api.Controllers.Portal;

/// <summary>
/// Progress (Module 3 — Progress): body-scan history, weight and composition trends, strength
/// curves off the workout log, the attendance calendar, progress photos and badges.
///
/// Scans the desk measured and scans the member typed in live in the same table and the same
/// chart — a member who weighs themselves at home should not have a second, parallel history
/// that the InBody trend ignores. Who recorded it is carried on the row instead.
/// </summary>
[Route("api/portal/progress")]
public class PortalProgressController : PortalControllerBase
{
    /// <summary>8 MB — a phone photo, not a camera raw.</summary>
    private const long MaxPhotoBytes = 8 * 1024 * 1024;

    private static readonly string[] PhotoTypes = { "image/jpeg", "image/png", "image/webp", "image/heic" };

    private readonly GymDbContext _db;
    private readonly PrivateMediaStorage _photos;
    private readonly TrainingService _training;
    private readonly IClock _clock;
    private readonly ILogger<PortalProgressController> _log;

    public PortalProgressController(
        GymDbContext db, PrivateMediaStorage photos, TrainingService training,
        IClock clock, ILogger<PortalProgressController> log)
    {
        _db = db;
        _photos = photos;
        _training = training;
        _clock = clock;
        _log = log;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PortalProgressResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PortalProgressResponse>> Get(CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();
        var today = _clock.Today;

        var scans = await _db.BodyScans
            .AsNoTracking()
            .Where(s => s.MemberId == memberId)
            .OrderBy(s => s.ScanDate)
            .ToListAsync(ct);

        var photos = await _db.ProgressPhotos
            .AsNoTracking()
            .Where(p => p.MemberId == memberId)
            .OrderByDescending(p => p.TakenOn)
            .Take(60)
            .ToListAsync(ct);

        var logs = await _db.WorkoutLogs
            .AsNoTracking()
            .Where(l => l.MemberId == memberId)
            .Select(l => new
            {
                l.ExerciseId, ExerciseName = l.Exercise.Name, ExerciseSlug = l.Exercise.Slug,
                l.Exercise.IsStrengthTracked, l.PerformedOn, l.WeightKg, l.Reps,
                l.EstimatedOneRepMax, l.Volume, l.IsPersonalRecord
            })
            .ToListAsync(ct);

        // One point per exercise per day: the top set. A chart of every warm-up set is noise.
        var strength = logs
            .Where(l => l.IsStrengthTracked && l.EstimatedOneRepMax > 0)
            .GroupBy(l => (l.ExerciseId, l.ExerciseName, l.ExerciseSlug))
            .Select(g =>
            {
                var points = g
                    .GroupBy(l => l.PerformedOn)
                    .Select(day =>
                    {
                        var top = day.OrderByDescending(l => l.EstimatedOneRepMax).First();
                        return new PortalStrengthPoint
                        {
                            Date = day.Key.ToString("yyyy-MM-dd"),
                            EstimatedOneRepMax = top.EstimatedOneRepMax,
                            TopSetWeightKg = top.WeightKg,
                            TopSetReps = top.Reps
                        };
                    })
                    .OrderBy(p => p.Date)
                    .ToList();

                return new PortalStrengthSeries
                {
                    ExerciseId = g.Key.ExerciseId,
                    ExerciseName = g.Key.ExerciseName,
                    Slug = g.Key.ExerciseSlug,
                    BestE1Rm = points.Max(p => p.EstimatedOneRepMax),
                    LatestE1Rm = points[^1].EstimatedOneRepMax,
                    Points = points
                };
            })
            .OrderByDescending(s => s.Points.Count)
            .Take(6)
            .ToList();

        var since = today.AddDays(-84);
        var weekly = logs
            .Where(l => l.PerformedOn >= since)
            .GroupBy(l => l.PerformedOn.AddDays(-((int)l.PerformedOn.DayOfWeek + 6) % 7))
            .OrderBy(g => g.Key)
            .Select(g => new PortalVolumePoint
            {
                WeekStarting = g.Key.ToString("yyyy-MM-dd"),
                Label = g.Key.ToString("dd MMM"),
                VolumeKg = decimal.Round(g.Sum(l => l.Volume), 0),
                Sets = g.Count()
            })
            .ToList();

        var badges = await _db.MemberBadges
            .AsNoTracking()
            .Include(mb => mb.Badge)
            .Where(mb => mb.MemberId == memberId)
            .OrderByDescending(mb => mb.AwardedAtUtc)
            .ToListAsync(ct);

        var first = scans.FirstOrDefault();
        var latest = scans.LastOrDefault();

        return Ok(new PortalProgressResponse
        {
            Scans = scans.OrderByDescending(s => s.ScanDate).Select(Describe).ToList(),
            Photos = photos.Select(Describe).ToList(),
            Strength = strength,
            WeeklyVolume = weekly,
            Streak = await PortalHomeController.LoadStreakAsync(_db, memberId, today, ct),
            Badges = badges.Select(mb => new PortalBadgeRow
            {
                Id = mb.BadgeId,
                Name = mb.Badge.Name,
                Slug = mb.Badge.Slug,
                Description = mb.Badge.Description,
                IconKey = mb.Badge.IconKey,
                Tier = mb.Badge.Tier,
                AwardedAtUtc = mb.AwardedAtUtc,
                IsSeen = mb.IsSeen
            }).ToList(),
            Headline = new PortalProgressHeadline
            {
                CurrentWeightKg = latest?.WeightKg,
                WeightChangeKg = first is null || latest is null || first.Id == latest.Id
                    ? null
                    : decimal.Round(latest.WeightKg - first.WeightKg, 1),
                CurrentBodyFatPercent = latest?.BodyFatPercent,
                BodyFatChange = first?.BodyFatPercent is { } startFat && latest?.BodyFatPercent is { } nowFat && first.Id != latest.Id
                    ? decimal.Round(nowFat - startFat, 1)
                    : null,
                MuscleMassChangeKg = first?.SkeletalMuscleMassKg is { } startMuscle
                                     && latest?.SkeletalMuscleMassKg is { } nowMuscle && first.Id != latest.Id
                    ? decimal.Round(nowMuscle - startMuscle, 1)
                    : null,
                ScanCount = scans.Count,
                FirstScanOn = first?.ScanDate.ToString("yyyy-MM-dd"),
                LatestScanOn = latest?.ScanDate.ToString("yyyy-MM-dd"),
                TotalPersonalRecords = logs.Count(l => l.IsPersonalRecord),
                TotalVolumeLiftedKg = decimal.Round(logs.Sum(l => l.Volume), 0)
            }
        });
    }

    /// <summary>
    /// Adds a scan the member took themselves. One per date: a second reading on the same day
    /// replaces the first rather than putting two points on the trend an hour apart.
    /// </summary>
    [HttpPost("scans")]
    [ProducesResponseType(typeof(PortalBodyScanRow), StatusCodes.Status201Created)]
    public async Task<ActionResult<PortalBodyScanRow>> AddScan(PortalBodyScanRequest request, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        if (request.ScanDate > _clock.Today)
            return Problem("A scan cannot be dated in the future.", statusCode: StatusCodes.Status400BadRequest);
        if (request.WeightKg is < 20 or > 400)
            return Problem("Weight has to be between 20 and 400 kg.", statusCode: StatusCodes.Status400BadRequest);
        if (request.BodyFatPercent is { } fat && fat is < 3 or > 70)
            return Problem("Body fat has to be between 3 and 70 percent.", statusCode: StatusCodes.Status400BadRequest);

        var member = await _db.Members.AsNoTracking().FirstAsync(m => m.Id == memberId, ct);

        var scan = await _db.BodyScans
            .FirstOrDefaultAsync(s => s.MemberId == memberId && s.ScanDate == request.ScanDate, ct);
        var isNew = scan is null;
        scan ??= new BodyScan { MemberId = memberId, ScanDate = request.ScanDate };

        scan.WeightKg = decimal.Round(request.WeightKg, 1);
        scan.BodyFatPercent = request.BodyFatPercent;
        scan.SkeletalMuscleMassKg = request.SkeletalMuscleMassKg;
        scan.FatMassKg = request.BodyFatPercent is { } percent
            ? decimal.Round(scan.WeightKg * percent / 100m, 1)
            : null;
        scan.VisceralFatLevel = request.VisceralFatLevel;
        scan.ChestCm = request.ChestCm;
        scan.WaistCm = request.WaistCm;
        scan.HipCm = request.HipCm;
        scan.ThighCm = request.ThighCm;
        scan.ArmCm = request.ArmCm;
        scan.Notes = request.Notes;
        scan.MeasuredBy = "Self";
        scan.DeviceName = null;

        // BMI is derived, never asked for — a member who mistypes it would corrupt their own trend.
        if (member.HeightCm is { } height && height > 0)
        {
            var metres = height / 100m;
            scan.Bmi = decimal.Round(scan.WeightKg / (metres * metres), 1);
        }

        if (isNew) _db.BodyScans.Add(scan);
        await _db.SaveChangesAsync(ct);

        return isNew
            ? Created($"/api/portal/progress/scans/{scan.Id}", Describe(scan))
            : Ok(Describe(scan));
    }

    /// <summary>Members may delete their own entries; a desk-measured scan is the gym's record.</summary>
    [HttpDelete("scans/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteScan(int id, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var scan = await _db.BodyScans.FirstOrDefaultAsync(s => s.Id == id && s.MemberId == memberId, ct);
        if (scan is null) return NotFound();
        if (!string.Equals(scan.MeasuredBy, "Self", StringComparison.OrdinalIgnoreCase))
            return Problem("That scan was taken at the gym. Ask the desk to correct it.",
                statusCode: StatusCodes.Status403Forbidden);

        _db.BodyScans.Remove(scan);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Uploads a progress photo. It is converted to WebP and stored outside wwwroot, so the
    /// only way to see it is through <see cref="PhotoFile"/> with this member's own token.
    /// </summary>
    [HttpPost("photos")]
    [RequestSizeLimit(MaxPhotoBytes)]
    [ProducesResponseType(typeof(PortalProgressPhotoRow), StatusCodes.Status201Created)]
    public async Task<ActionResult<PortalProgressPhotoRow>> UploadPhoto(
        IFormFile file, [FromForm] string? pose, [FromForm] string? takenOn,
        [FromForm] decimal? weightKg, [FromForm] string? notes, CancellationToken ct = default)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        if (file is null || file.Length == 0)
            return Problem("Pick a photo first.", statusCode: StatusCodes.Status400BadRequest);
        if (file.Length > MaxPhotoBytes)
            return Problem("That photo is over 8 MB. Most phones can send a smaller copy.",
                statusCode: StatusCodes.Status400BadRequest);
        if (!PhotoTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return Problem("Photos only — JPEG, PNG or WebP.", statusCode: StatusCodes.Status400BadRequest);

        var poseValue = (pose ?? "front").Trim().ToLowerInvariant();
        if (poseValue is not ("front" or "side" or "back")) poseValue = "front";

        var date = DateOnly.TryParse(takenOn, out var parsed) ? parsed : _clock.Today;
        if (date > _clock.Today) date = _clock.Today;

        // Random stem: the filename is never derived from the member, and never guessable.
        var stem = $"{Guid.NewGuid():N}";
        await using var stream = file.OpenReadStream();

        StoredMedia stored;
        try
        {
            stored = await _photos.SaveImageAsync(stream, $"{stem}.webp", $"progress/{memberId}", ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "Progress photo upload failed for member {MemberId}", memberId);
            return Problem("That file could not be read as an image.", statusCode: StatusCodes.Status400BadRequest);
        }

        var photo = new ProgressPhoto
        {
            MemberId = memberId,
            TakenOn = date,
            ImageUrl = stored.OriginalUrl,
            Pose = poseValue,
            WeightKg = weightKg,
            IsPrivate = true,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
        _db.ProgressPhotos.Add(photo);
        await _db.SaveChangesAsync(ct);

        return Created($"/api/portal/progress/photos/{photo.Id}", Describe(photo));
    }

    /// <summary>
    /// Streams one photo. This is the only route to the bytes: the store is not statically
    /// served, so ownership is checked on every single read rather than assumed from the URL.
    /// </summary>
    [HttpGet("photos/{id:int}/file")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> PhotoFile(int id, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var photo = await _db.ProgressPhotos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.MemberId == memberId, ct);
        if (photo is null) return NotFound();

        var path = _photos.ResolvePath(photo.ImageUrl);
        if (path is null || !System.IO.File.Exists(path)) return NotFound();

        // Private: cached by the member's own browser, never by a shared proxy.
        Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
        {
            Private = true,
            NoStore = false,
            MaxAge = TimeSpan.FromHours(6)
        };
        return PhysicalFile(path, "image/webp");
    }

    [HttpDelete("photos/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeletePhoto(int id, CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var photo = await _db.ProgressPhotos.FirstOrDefaultAsync(p => p.Id == id && p.MemberId == memberId, ct);
        if (photo is null) return NotFound();

        try
        {
            await _photos.DeleteAsync(photo.ImageUrl, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The row is what the member sees; an orphaned file is a cleanup job, not a failure.
            _log.LogWarning(ex, "Could not delete progress photo file {Url}", photo.ImageUrl);
        }

        _db.ProgressPhotos.Remove(photo);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Marks badges as seen so the "new" ring stops after the member has looked.</summary>
    [HttpPost("badges/seen")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkBadgesSeen(CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var unseen = await _db.MemberBadges
            .Where(mb => mb.MemberId == memberId && !mb.IsSeen)
            .ToListAsync(ct);
        foreach (var badge in unseen) badge.IsSeen = true;

        if (unseen.Count > 0) await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Re-runs the badge rules — cheap, and it catches anything earned before this build.</summary>
    [HttpPost("badges/refresh")]
    [ProducesResponseType(typeof(IReadOnlyList<PortalBadgeRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PortalBadgeRow>>> RefreshBadges(CancellationToken ct)
    {
        if (CurrentMemberId is not { } memberId) return NoMemberProfile();

        var awarded = await _training.AwardBadgesAsync(memberId, ct);
        if (awarded.Count == 0) return Ok(Array.Empty<PortalBadgeRow>());

        var ids = awarded.Select(a => a.BadgeId).ToList();
        var badges = await _db.Badges.AsNoTracking().Where(b => ids.Contains(b.Id)).ToListAsync(ct);

        return Ok(awarded.Select(a =>
        {
            var badge = badges.First(b => b.Id == a.BadgeId);
            return new PortalBadgeRow
            {
                Id = badge.Id,
                Name = badge.Name,
                Slug = badge.Slug,
                Description = badge.Description,
                IconKey = badge.IconKey,
                Tier = badge.Tier,
                AwardedAtUtc = a.AwardedAtUtc,
                IsSeen = false
            };
        }).ToList());
    }

    // ---------------------------------------------------------------- helpers

    private static PortalBodyScanRow Describe(BodyScan s) => new()
    {
        Id = s.Id,
        ScanDate = s.ScanDate.ToString("yyyy-MM-dd"),
        WeightKg = s.WeightKg,
        BodyFatPercent = s.BodyFatPercent,
        SkeletalMuscleMassKg = s.SkeletalMuscleMassKg,
        FatMassKg = s.FatMassKg,
        VisceralFatLevel = s.VisceralFatLevel,
        Bmi = s.Bmi,
        BasalMetabolicRate = s.BasalMetabolicRate,
        TotalBodyWaterL = s.TotalBodyWaterL,
        InBodyScore = s.InBodyScore,
        ChestCm = s.ChestCm,
        WaistCm = s.WaistCm,
        HipCm = s.HipCm,
        ThighCm = s.ThighCm,
        ArmCm = s.ArmCm,
        MeasuredBy = s.MeasuredBy,
        DeviceName = s.DeviceName,
        Notes = s.Notes,
        IsSelfReported = string.Equals(s.MeasuredBy, "Self", StringComparison.OrdinalIgnoreCase)
    };

    private static PortalProgressPhotoRow Describe(ProgressPhoto p) => new()
    {
        Id = p.Id,
        TakenOn = p.TakenOn.ToString("yyyy-MM-dd"),
        // The client never sees the storage path — only the authorised route to the bytes.
        ImageUrl = $"/api/portal/progress/photos/{p.Id}/file",
        Pose = p.Pose,
        WeightKg = p.WeightKg,
        Notes = p.Notes
    };
}
