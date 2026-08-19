using System.Text.Json;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.Services;

/// <summary>What one logged set turned out to be worth: the row, and whether it beat anything.</summary>
public record SetOutcome(WorkoutLog Log, bool IsPersonalRecord, decimal? PreviousBestE1Rm, string ExerciseName);

/// <summary>
/// Set logging and the PR engine.
///
/// Records are compared on estimated 1RM (Epley) rather than on raw weight, because a member
/// who moves 60 kg for 8 has beaten their 65 kg single and a weight-only comparison would tell
/// them they had not. The estimate is stored on the row so the chart and the record both read
/// the same number years later, even if the formula is ever changed.
/// </summary>
public class TrainingService
{
    private readonly GymDbContext _db;
    private readonly INotificationDispatcher _notifier;
    private readonly IClock _clock;
    private readonly ILogger<TrainingService> _log;

    public TrainingService(
        GymDbContext db, INotificationDispatcher notifier, IClock clock, ILogger<TrainingService> log)
    {
        _db = db;
        _notifier = notifier;
        _clock = clock;
        _log = log;
    }

    /// <summary>Epley. Reps are capped at 12 — beyond that the estimate stops meaning anything.</summary>
    public static decimal EstimateOneRepMax(decimal weightKg, int reps)
    {
        if (weightKg <= 0 || reps <= 0) return 0m;
        var counted = Math.Min(reps, 12);
        return decimal.Round(weightKg * (1 + counted / 30m), 1);
    }

    /// <summary>
    /// Writes one set and decides whether it is a record. A PR raises the celebration flag on
    /// the row (the banner reads it, then marks it seen), fires the notification, and posts to
    /// the community feed only when the member has opted the leaderboard in — a record is the
    /// member's to publish, not ours.
    /// </summary>
    public async Task<SetOutcome> LogSetAsync(
        int memberId, int exerciseId, int? programExerciseId, DateOnly performedOn,
        int setNumber, int reps, decimal weightKg, int? rpe, int? durationSeconds, decimal? distanceKm,
        string? notes, CancellationToken ct = default)
    {
        var exercise = await _db.Exercises.AsNoTracking().FirstOrDefaultAsync(e => e.Id == exerciseId, ct)
            ?? throw new InvalidOperationException("That exercise is not in the library.");

        var member = await _db.Members.FirstAsync(m => m.Id == memberId, ct);
        var e1rm = EstimateOneRepMax(weightKg, reps);

        // Only loaded, strength-tracked lifts carry records; a 3-minute plank is not a 1RM.
        var eligible = exercise.IsStrengthTracked && weightKg > 0 && reps > 0;

        decimal? previousBest = null;
        if (eligible)
        {
            previousBest = await _db.WorkoutLogs
                .Where(l => l.MemberId == memberId && l.ExerciseId == exerciseId)
                .MaxAsync(l => (decimal?)l.EstimatedOneRepMax, ct);
        }

        var isPr = eligible && e1rm > (previousBest ?? 0m);

        var log = new WorkoutLog
        {
            MemberId = memberId,
            ExerciseId = exerciseId,
            ProgramExerciseId = programExerciseId,
            PerformedOn = performedOn,
            PerformedAtUtc = _clock.UtcNow,
            SetNumber = setNumber,
            Reps = reps,
            WeightKg = weightKg,
            Rpe = rpe,
            DurationSeconds = durationSeconds,
            DistanceKm = distanceKm,
            Volume = decimal.Round(weightKg * reps, 2),
            EstimatedOneRepMax = e1rm,
            IsPersonalRecord = isPr,
            PrCelebrated = false,
            Notes = notes,
            CreatedBy = member.MemberCode
        };

        _db.WorkoutLogs.Add(log);
        await _db.SaveChangesAsync(ct);

        if (isPr)
        {
            _log.LogInformation("PR for {Member} on {Exercise}: {E1rm} kg e1RM (was {Previous})",
                member.MemberCode, exercise.Name, e1rm, previousBest);

            var gain = previousBest is > 0 ? e1rm - previousBest.Value : (decimal?)null;

            await _notifier.SendAsync(new OutboundMessage
            {
                MemberId = memberId,
                Kind = NotificationKind.PersonalRecord,
                Title = $"New {exercise.Name} record",
                Body = gain is null
                    ? $"{weightKg:0.#} kg × {reps} — your first logged record on this lift."
                    : $"{weightKg:0.#} kg × {reps} — {gain:0.#} kg better than your last best.",
                ActionUrl = "/portal/workouts",
                TemplateKey = "pr.detected",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    exercise = exercise.Name,
                    weightKg,
                    reps,
                    estimatedOneRepMax = e1rm,
                    previousBest
                })
            }, ct);

            if (member.ConsentLeaderboard)
            {
                _db.FeedPosts.Add(new FeedPost
                {
                    Kind = FeedPostKind.PersonalRecord,
                    MemberId = memberId,
                    BranchId = member.HomeBranchId,
                    Title = $"{member.FullName.Split(' ')[0]} set a {exercise.Name} record",
                    Body = $"{weightKg:0.#} kg × {reps}",
                    MetaJson = JsonSerializer.Serialize(new { lift = exercise.Name, weightKg, reps, e1rm, previousBest }),
                    PostedAtUtc = _clock.UtcNow
                });
                await _db.SaveChangesAsync(ct);
            }

            await AwardBadgesAsync(memberId, ct);
        }

        return new SetOutcome(log, isPr, previousBest, exercise.Name);
    }

    /// <summary>
    /// Awards any badge whose rule the member now satisfies. Rules are the machine-readable
    /// <c>CriteriaJson</c> the badge catalogue already carries, so adding a badge is a data
    /// change rather than a deployment.
    /// </summary>
    public async Task<IReadOnlyList<MemberBadge>> AwardBadgesAsync(int memberId, CancellationToken ct = default)
    {
        var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == memberId, ct);
        if (member is null) return Array.Empty<MemberBadge>();

        var badges = await _db.Badges.AsNoTracking().Where(b => b.IsActive).ToListAsync(ct);
        if (badges.Count == 0) return Array.Empty<MemberBadge>();

        var held = await _db.MemberBadges
            .Where(mb => mb.MemberId == memberId)
            .Select(mb => mb.BadgeId)
            .ToListAsync(ct);
        var candidates = badges.Where(b => !held.Contains(b.Id)).ToList();
        if (candidates.Count == 0) return Array.Empty<MemberBadge>();

        var facts = new BadgeFacts
        {
            CheckIns = await _db.CheckIns.CountAsync(c => c.MemberId == memberId && !c.WasBlocked, ct),
            ClassesAttended = await _db.Bookings.CountAsync(b => b.MemberId == memberId && b.Status == BookingStatus.Attended, ct),
            PersonalRecords = await _db.WorkoutLogs.CountAsync(l => l.MemberId == memberId && l.IsPersonalRecord, ct),
            CurrentStreak = member.CurrentStreakDays,
            LongestStreak = member.LongestStreakDays,
            ReferralsConverted = await _db.Referrals.CountAsync(r =>
                r.ReferrerMemberId == memberId &&
                (r.Status == ReferralStatus.Converted || r.Status == ReferralStatus.Rewarded), ct),
            // The scale beats the joining form: a lift ratio should use what the member weighs now.
            BodyWeightKg = await _db.BodyScans
                .Where(s => s.MemberId == memberId)
                .OrderByDescending(s => s.ScanDate)
                .Select(s => (decimal?)s.WeightKg)
                .FirstOrDefaultAsync(ct) ?? member.StartWeightKg,
            BestByExerciseSlug = await _db.WorkoutLogs
                .Where(l => l.MemberId == memberId)
                .GroupBy(l => l.Exercise.Slug)
                .Select(g => new { Slug = g.Key, Best = g.Max(l => l.WeightKg) })
                .ToDictionaryAsync(x => x.Slug, x => x.Best, ct)
        };

        var awarded = new List<MemberBadge>();
        foreach (var badge in candidates)
        {
            if (!Qualifies(badge.CriteriaJson, facts)) continue;

            var row = new MemberBadge
            {
                MemberId = memberId,
                BadgeId = badge.Id,
                AwardedAtUtc = _clock.UtcNow,
                Context = badge.Name,
                IsSeen = false
            };
            _db.MemberBadges.Add(row);
            awarded.Add(row);
        }

        if (awarded.Count == 0) return Array.Empty<MemberBadge>();

        await _db.SaveChangesAsync(ct);
        await _notifier.SendManyAsync(awarded.Select(a =>
        {
            var badge = candidates.First(b => b.Id == a.BadgeId);
            return new OutboundMessage
            {
                MemberId = memberId,
                Kind = NotificationKind.StreakMilestone,
                Title = $"Badge unlocked — {badge.Name}",
                Body = badge.Description,
                ActionUrl = "/portal/progress",
                TemplateKey = "badge.awarded"
            };
        }), ct);

        return awarded;
    }

    /// <summary>Everything the badge rules can be evaluated against, read once per award pass.</summary>
    private sealed record BadgeFacts
    {
        public int CheckIns { get; init; }
        public int ClassesAttended { get; init; }
        public int PersonalRecords { get; init; }
        public int CurrentStreak { get; init; }
        public int LongestStreak { get; init; }
        public int ReferralsConverted { get; init; }
        public decimal? BodyWeightKg { get; init; }
        public IReadOnlyDictionary<string, decimal> BestByExerciseSlug { get; init; } =
            new Dictionary<string, decimal>();
    }

    /// <summary>
    /// Reads a badge rule. An unparseable or unknown rule never awards — a badge handed out by
    /// accident cannot be taken back without the member noticing.
    /// </summary>
    private static bool Qualifies(string criteriaJson, BadgeFacts facts)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(criteriaJson) ? "{}" : criteriaJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("metric", out var metricElement)) return false;
            if (!root.TryGetProperty("threshold", out var thresholdElement)) return false;
            if (!thresholdElement.TryGetDecimal(out var threshold)) return false;

            var exerciseSlug = root.TryGetProperty("exercise", out var e) ? e.GetString() : null;
            decimal? best = exerciseSlug is not null && facts.BestByExerciseSlug.TryGetValue(exerciseSlug, out var lift)
                ? lift
                : null;

            return metricElement.GetString() switch
            {
                "checkIns" => facts.CheckIns >= threshold,
                "classesAttended" => facts.ClassesAttended >= threshold,
                "personalRecords" => facts.PersonalRecords >= threshold,
                "streakDays" => Math.Max(facts.CurrentStreak, facts.LongestStreak) >= threshold,
                "referralsConverted" => facts.ReferralsConverted >= threshold,
                "liftAbsolute" => best is { } kg && kg >= threshold,
                // Needs a bodyweight to divide by; with none on file the badge simply waits.
                "liftRatio" => best is { } lifted && facts.BodyWeightKg is > 0 &&
                               lifted / facts.BodyWeightKg.Value >= threshold,
                _ => false
            };
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// The streak half of the PR/streak engine (Module 4.5). Called when a visit advances a
    /// member's streak: awards whatever badge the new number qualifies for, and posts the
    /// round-number milestones to the community feed.
    ///
    /// Only milestones post. A feed that announces every consecutive day is a feed nobody
    /// reads by the end of the week, and it buries the records it exists to celebrate.
    /// </summary>
    public async Task<bool> RecordStreakMilestoneAsync(int memberId, CancellationToken ct = default)
    {
        var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == memberId, ct);
        if (member is null) return false;

        await AwardBadgesAsync(memberId, ct);

        var streak = member.CurrentStreakDays;
        if (streak is not (7 or 14 or 30 or 50 or 100 or 200 or 365)) return false;

        await _notifier.SendAsync(new OutboundMessage
        {
            MemberId = memberId,
            Kind = NotificationKind.StreakMilestone,
            Title = $"{streak}-day streak",
            Body = $"{streak} days in a row. That is the part most people never get to.",
            ActionUrl = "/portal/progress",
            TemplateKey = "streak.milestone",
            PayloadJson = JsonSerializer.Serialize(new { streak })
        }, ct);

        if (member.ConsentLeaderboard)
        {
            _db.FeedPosts.Add(new FeedPost
            {
                Kind = FeedPostKind.Milestone,
                MemberId = memberId,
                BranchId = member.HomeBranchId,
                Title = $"{member.FullName.Split(' ')[0]} hit a {streak}-day streak",
                Body = $"{streak} consecutive training days.",
                MetaJson = JsonSerializer.Serialize(new { streak, longest = member.LongestStreakDays }),
                PostedAtUtc = _clock.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }

        return true;
    }
}
