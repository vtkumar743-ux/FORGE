using Gym.Core.Entities;
using Gym.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Gym.Infrastructure.Persistence.Seeding;

/// <summary>
/// Attendance, bookings, training logs and CRM activity. Everything is derived from a
/// per-member visit frequency so streaks, churn scores and occupancy agree with each other
/// instead of being three unrelated random numbers.
/// </summary>
internal static class ActivitySeed
{
    public static async Task CheckInsAndStreaksAsync(
        GymDbContext db, List<Member> members, DateOnly today, int daysBack, Random rng, CancellationToken ct)
    {
        if (await db.CheckIns.AnyAsync(ct)) return;

        var checkIns = new List<CheckIn>();

        foreach (var member in members)
        {
            // Visits per week by member state — the single input the rest of the demo derives from.
            var perWeek = member.Status switch
            {
                MemberStatus.Active => WeightedFrequency(rng),
                MemberStatus.Trial => rng.Next(1, 4),
                MemberStatus.Frozen => 0,
                MemberStatus.Expired => 0,
                _ => 0
            };

            var window = Math.Min(daysBack, Math.Max(0, today.DayNumber - member.JoinedOn.DayNumber));
            if (window == 0) continue;

            // Expired/cancelled members trained, then stopped — that gap is what the churn radar reads.
            var stopDaysAgo = member.Status switch
            {
                MemberStatus.Expired => rng.Next(12, 70),
                MemberStatus.Cancelled => rng.Next(25, 100),
                MemberStatus.Frozen => rng.Next(4, 24),
                _ => 0
            };
            var historicPerWeek = perWeek == 0 ? rng.Next(1, 5) : perWeek;

            var visitDates = new List<DateOnly>();
            for (var offset = window; offset >= 0; offset--)
            {
                var date = today.AddDays(-offset);
                if (stopDaysAgo > 0 && offset < stopDaysAgo) continue;

                var rate = (perWeek == 0 ? historicPerWeek : perWeek) / 7.0;
                // Sunday is genuinely quieter; weekday evenings carry the load.
                if (date.DayOfWeek == DayOfWeek.Sunday) rate *= 0.45;
                if (rng.NextDouble() < rate) visitDates.Add(date);
            }

            // Give the top of the roster a clean unbroken run so the streak flame has real data.
            if (member.Status == MemberStatus.Active && perWeek >= 5 && rng.NextDouble() < 0.35)
                for (var d = 0; d < rng.Next(9, 26); d++)
                {
                    var date = today.AddDays(-d);
                    if (!visitDates.Contains(date)) visitDates.Add(date);
                }

            foreach (var date in visitDates.Distinct().OrderBy(d => d))
            {
                var hour = PickVisitHour(date, rng);
                var localStart = date.ToDateTime(new TimeOnly(hour, rng.Next(0, 60)));
                var checkIn = new CheckIn
                {
                    MemberId = member.Id,
                    BranchId = member.HomeBranchId,
                    VisitDate = date,
                    CheckInAtUtc = localStart.AddMinutes(-330),
                    CheckOutAtUtc = localStart.AddMinutes(rng.Next(42, 105) - 330),
                    Source = rng.NextDouble() < 0.86 ? CheckInSource.Qr : CheckInSource.Manual,
                    DeviceId = $"kiosk-{member.HomeBranchId:D2}",
                    CreatedAtUtc = localStart.AddMinutes(-330)
                };
                checkIns.Add(checkIn);
            }

            member.LastVisitOn = visitDates.Count > 0 ? visitDates.Max() : null;
            (member.CurrentStreakDays, member.LongestStreakDays) = Streaks(visitDates, today);
        }

        // Members currently on the floor: open rows (no check-out) are what the occupancy meter counts.
        var onFloor = members.Where(m => m.Status == MemberStatus.Active).OrderBy(_ => rng.Next()).Take(46).ToList();
        foreach (var member in onFloor)
        {
            var minutesAgo = rng.Next(5, 70);
            checkIns.Add(new CheckIn
            {
                MemberId = member.Id,
                BranchId = member.HomeBranchId,
                VisitDate = today,
                CheckInAtUtc = DateTime.UtcNow.AddMinutes(-minutesAgo),
                CheckOutAtUtc = null,
                Source = CheckInSource.Qr,
                DeviceId = $"kiosk-{member.HomeBranchId:D2}"
            });
            member.LastVisitOn = today;
        }

        db.CheckIns.AddRange(checkIns);
        await db.SaveChangesAsync(ct);

        ScoreChurn(members, today);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Rules-based churn scoring (differentiator #3): visit-frequency drop, unpaid invoice,
    /// no forward bookings and an expiring term each add weight.
    /// </summary>
    private static void ScoreChurn(List<Member> members, DateOnly today)
    {
        foreach (var member in members)
        {
            var score = 0;
            var daysSinceVisit = member.LastVisitOn is null
                ? 999
                : today.DayNumber - member.LastVisitOn.Value.DayNumber;

            score += daysSinceVisit switch
            {
                <= 3 => 0, <= 7 => 8, <= 14 => 25, <= 21 => 42, <= 45 => 60, _ => 75
            };
            if (member.CurrentStreakDays == 0) score += 6;
            if (member.Status == MemberStatus.Frozen) score += 12;
            if (member.Status is MemberStatus.Expired or MemberStatus.Cancelled) score = Math.Min(100, score + 15);

            member.ChurnScore = Math.Clamp(score, 0, 100);
            member.ChurnRisk = member.ChurnScore switch
            {
                < 20 => ChurnRiskBand.Healthy,
                < 40 => ChurnRiskBand.Watch,
                < 65 => ChurnRiskBand.Amber,
                _ => ChurnRiskBand.Red
            };
        }
    }

    public static async Task BookingsAsync(
        GymDbContext db, List<Member> members, List<ClassSession> sessions, DateOnly today, Random rng, CancellationToken ct)
    {
        if (await db.Bookings.AnyAsync(ct)) return;

        var membersByBranch = members
            .Where(m => m.Status is MemberStatus.Active or MemberStatus.Trial)
            .GroupBy(m => m.HomeBranchId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var bookings = new List<Booking>();

        foreach (var session in sessions)
        {
            if (!membersByBranch.TryGetValue(session.BranchId, out var pool) || pool.Count == 0) continue;

            // Evening peak fills; the 10:30 off-peak slot runs light. This is what makes the
            // capacity ring and the "typical busy hours" chart look like a real gym.
            var fillRate = session.StartTime.Hour switch
            {
                <= 6 => 0.70,
                <= 8 => 0.82,
                <= 11 => 0.34,
                <= 17 => 0.45,
                <= 19 => 0.95,
                _ => 0.78
            };
            if (session.SessionDate.DayOfWeek == DayOfWeek.Sunday) fillRate *= 0.6;

            var wanted = (int)Math.Round(session.Capacity * fillRate * (0.85 + rng.NextDouble() * 0.3));
            wanted = Math.Clamp(wanted, 0, Math.Min(pool.Count, session.Capacity + 6));
            if (wanted == 0) continue;

            var attendees = pool.OrderBy(_ => rng.Next()).Take(wanted).ToList();
            var booked = 0;
            var waitlisted = 0;
            var attended = 0;
            var isPast = session.SessionDate < today;

            foreach (var member in attendees)
            {
                var overflow = booked >= session.Capacity;
                var status = BookingStatus.Booked;

                if (overflow)
                {
                    if (waitlisted >= 10) continue;
                    status = BookingStatus.Waitlisted;
                }
                else if (isPast)
                {
                    // Real no-show rate sits around 10%; a few cancel outright.
                    var roll = rng.NextDouble();
                    status = roll switch { < 0.855 => BookingStatus.Attended, < 0.955 => BookingStatus.NoShow, _ => BookingStatus.Cancelled };
                }

                var bookedAt = session.StartsAtUtc.AddHours(-rng.Next(2, 60));
                var booking = new Booking
                {
                    ClassSessionId = session.Id,
                    MemberId = member.Id,
                    Status = status,
                    BookedAtUtc = bookedAt,
                    CreatedAtUtc = bookedAt,
                    CreditConsumed = true
                };

                switch (status)
                {
                    case BookingStatus.Waitlisted:
                        booking.WaitlistPosition = ++waitlisted;
                        break;
                    case BookingStatus.Cancelled:
                        booking.CancelledAtUtc = session.StartsAtUtc.AddHours(-rng.Next(1, 20));
                        booking.WasLateCancel = rng.NextDouble() < 0.3;
                        break;
                    case BookingStatus.Attended:
                        booking.CheckedInAtUtc = session.StartsAtUtc.AddMinutes(-rng.Next(2, 18));
                        attended++;
                        booked++;
                        // Post-class rating prompt: about half of attendees respond.
                        if (rng.NextDouble() < 0.48)
                        {
                            booking.RatingScore = rng.NextDouble() switch { < 0.72 => 5, < 0.92 => 4, < 0.98 => 3, _ => 2 };
                            if (booking.RatingScore <= 3)
                                booking.RatingComment = "Room was too crowded for the warm-up.";
                        }
                        break;
                    default:
                        booked++;
                        break;
                }

                bookings.Add(booking);
            }

            session.BookedCount = booked;
            session.WaitlistCount = waitlisted;
            session.AttendedCount = attended;
        }

        db.Bookings.AddRange(bookings);
        await db.SaveChangesAsync(ct);
    }

    public static async Task TrainingHistoryAsync(
        GymDbContext db, List<Member> members, List<Exercise> exercises, List<Trainer> trainers,
        DateOnly today, Random rng, CancellationToken ct)
    {
        if (await db.WorkoutLogs.AnyAsync(ct)) return;

        var tracked = exercises.Where(e => e.IsStrengthTracked && e.Equipment == EquipmentKind.Barbell).ToList();
        var logs = new List<WorkoutLog>();
        var scans = new List<BodyScan>();

        // The members who actually log: committed, recent, active.
        var loggers = members
            .Where(m => m.Status == MemberStatus.Active && m.CurrentStreakDays > 0)
            .OrderByDescending(m => m.LongestStreakDays)
            .Take(72)
            .ToList();

        foreach (var member in loggers)
        {
            var lifts = tracked.OrderBy(_ => rng.Next()).Take(rng.Next(3, 5)).ToList();
            var isFemale = member.Gender == Gender.Female;

            foreach (var lift in lifts)
            {
                // Start from a plausible working weight and progress a little each week.
                var start = lift.Name switch
                {
                    "Back Squat" => isFemale ? 32m : 55m,
                    "Conventional Deadlift" => isFemale ? 40m : 70m,
                    "Barbell Bench Press" => isFemale ? 20m : 42m,
                    "Overhead Press" => isFemale ? 15m : 30m,
                    "Front Squat" => isFemale ? 25m : 45m,
                    "Barbell Row" => isFemale ? 22m : 40m,
                    "Romanian Deadlift" => isFemale ? 30m : 55m,
                    _ => isFemale ? 20m : 35m
                };

                var best = 0m;
                var weeks = rng.Next(6, 15);
                for (var week = 0; week < weeks; week++)
                {
                    var date = today.AddDays(-(weeks - week) * 7 + rng.Next(0, 3));
                    if (date < member.JoinedOn) continue;

                    var working = start + week * (isFemale ? 1.5m : 2.5m);
                    var topSetReps = rng.Next(3, 9);

                    for (var set = 1; set <= 3; set++)
                    {
                        var weight = decimal.Round(working - (3 - set) * 2.5m, 1);
                        var reps = set == 3 ? topSetReps : topSetReps + 2;
                        // Epley: the comparison the PR engine uses across different rep counts.
                        var e1rm = decimal.Round(weight * (1 + reps / 30m), 1);
                        var isPr = e1rm > best;
                        if (isPr) best = e1rm;

                        logs.Add(new WorkoutLog
                        {
                            MemberId = member.Id,
                            ExerciseId = lift.Id,
                            PerformedOn = date,
                            PerformedAtUtc = date.ToDateTime(new TimeOnly(19, 0)).AddMinutes(-330),
                            SetNumber = set,
                            Reps = reps,
                            WeightKg = weight,
                            Rpe = rng.Next(6, 10),
                            Volume = weight * reps,
                            EstimatedOneRepMax = e1rm,
                            IsPersonalRecord = isPr,
                            PrCelebrated = isPr && date < today.AddDays(-1),
                            CreatedAtUtc = date.ToDateTime(new TimeOnly(19, 5)).AddMinutes(-330)
                        });
                    }
                }
            }

            // Body-composition history — 2 to 6 scans, trending the right way.
            var scanCount = rng.Next(2, 7);
            var scanWeight = member.StartWeightKg ?? 72m;
            var fat = isFemale ? rng.Next(26, 39) : rng.Next(18, 33);
            for (var s = 0; s < scanCount; s++)
            {
                var date = today.AddDays(-(scanCount - s) * 45 + rng.Next(0, 8));
                if (date < member.JoinedOn) continue;
                scanWeight -= (decimal)(rng.NextDouble() * 1.8);
                fat -= rng.Next(0, 2);
                var heightM = (member.HeightCm ?? 170m) / 100m;

                scans.Add(new BodyScan
                {
                    MemberId = member.Id,
                    ScanDate = date,
                    WeightKg = decimal.Round(scanWeight, 1),
                    BodyFatPercent = Math.Max(9, fat),
                    SkeletalMuscleMassKg = decimal.Round(scanWeight * (isFemale ? 0.40m : 0.46m), 1),
                    FatMassKg = decimal.Round(scanWeight * fat / 100m, 1),
                    VisceralFatLevel = rng.Next(4, 14),
                    Bmi = decimal.Round(scanWeight / (heightM * heightM), 1),
                    BasalMetabolicRate = decimal.Round(scanWeight * (isFemale ? 21.5m : 23.8m), 0),
                    TotalBodyWaterL = decimal.Round(scanWeight * 0.56m, 1),
                    InBodyScore = rng.Next(68, 92),
                    WaistCm = decimal.Round(scanWeight * (isFemale ? 0.95m : 1.02m), 1),
                    MeasuredBy = trainers[rng.Next(trainers.Count)].FullName,
                    DeviceName = "InBody 570",
                    CreatedAtUtc = date.ToDateTime(new TimeOnly(9, 0)).AddMinutes(-330)
                });
            }
        }

        db.WorkoutLogs.AddRange(logs);
        db.BodyScans.AddRange(scans);
        await db.SaveChangesAsync(ct);
    }

    public static async Task CrmAndEngagementAsync(
        GymDbContext db, List<Branch> branches, List<Member> members, List<Plan> plans,
        DateOnly today, Random rng, CancellationToken ct)
    {
        if (!await db.Leads.AnyAsync(ct))
        {
            var leads = new List<Lead>();
            var stages = new[]
            {
                (LeadStage.Inquiry, 16), (LeadStage.Tour, 9), (LeadStage.Trial, 8),
                (LeadStage.Negotiation, 5), (LeadStage.Joined, 7), (LeadStage.Lost, 6)
            };

            foreach (var (stage, count) in stages)
                for (var i = 0; i < count; i++)
                {
                    var isFemale = rng.NextDouble() < 0.45;
                    var first = isFemale
                        ? SeedPools.FemaleFirstNames[rng.Next(SeedPools.FemaleFirstNames.Length)]
                        : SeedPools.MaleFirstNames[rng.Next(SeedPools.MaleFirstNames.Length)];
                    var last = SeedPools.LastNames[rng.Next(SeedPools.LastNames.Length)];
                    var branch = branches[rng.Next(branches.Count)];
                    var createdDaysAgo = stage switch
                    {
                        LeadStage.Inquiry => rng.Next(0, 5),
                        LeadStage.Tour => rng.Next(2, 10),
                        LeadStage.Trial => rng.Next(3, 14),
                        LeadStage.Negotiation => rng.Next(5, 20),
                        _ => rng.Next(10, 60)
                    };
                    var created = DateTime.UtcNow.AddDays(-createdDaysAgo).AddHours(-rng.Next(0, 10));

                    var lead = new Lead
                    {
                        FullName = $"{first} {last}",
                        Phone = $"+919{rng.Next(100000000, 999999999)}",
                        Email = $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}@example.com",
                        BranchId = branch.Id,
                        Stage = stage,
                        Source = (LeadSource)rng.Next(0, 6),
                        Goal = SeedPools.Goals[rng.Next(SeedPools.Goals.Length)],
                        PreferredTime = rng.Next(2) == 0 ? "Weekday evening after 7 PM" : "Weekday morning before 8 AM",
                        InterestedPlanId = plans[rng.Next(plans.Count)].Id,
                        AssignedTo = "Front desk",
                        CreatedAtUtc = created,
                        // Benchmark from the research: first response inside five minutes.
                        FirstResponseAtUtc = stage == LeadStage.Inquiry && rng.NextDouble() < 0.3
                            ? null
                            : created.AddMinutes(rng.Next(2, 24)),
                        SequenceActive = stage is not (LeadStage.Joined or LeadStage.Lost),
                        SequenceStep = Math.Min(4, (int)stage),
                        UtmSource = rng.Next(3) == 0 ? "google" : null
                    };

                    if (stage == LeadStage.Trial) lead.TrialRequestedFor = today.AddDays(rng.Next(-2, 5));
                    if (stage == LeadStage.Joined)
                    {
                        lead.ConvertedOn = today.AddDays(-rng.Next(1, 25));
                        lead.SequenceActive = false;
                    }
                    if (stage == LeadStage.Lost)
                        lead.LostReason = rng.Next(3) switch
                        {
                            0 => "Chose a cheaper gym near home",
                            1 => "Stopped responding after the tour",
                            _ => "Moving out of the city"
                        };
                    if (lead.SequenceActive)
                        lead.NextFollowUpAtUtc = DateTime.UtcNow.AddHours(rng.Next(-18, 48));

                    var followUpCount = stage switch
                    {
                        LeadStage.Inquiry => 1, LeadStage.Tour => 2, LeadStage.Trial => 3,
                        LeadStage.Negotiation => 4, _ => 3
                    };
                    for (var f = 0; f < followUpCount; f++)
                    {
                        var due = created.AddDays(f * 2).AddHours(rng.Next(1, 9));
                        lead.FollowUps.Add(new LeadFollowUp
                        {
                            Channel = f == 0 ? FollowUpChannel.WhatsApp : (FollowUpChannel)rng.Next(0, 4),
                            DueAtUtc = due,
                            CompletedAtUtc = due < DateTime.UtcNow.AddHours(-2) ? due.AddMinutes(rng.Next(5, 90)) : null,
                            IsAutomated = f == 0,
                            StageAtCreation = stage,
                            Owner = "Front desk",
                            Outcome = due < DateTime.UtcNow.AddHours(-2) ? "Spoke, follow-up scheduled" : null,
                            CreatedAtUtc = created
                        });
                    }

                    leads.Add(lead);
                }

            db.Leads.AddRange(leads);
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Referrals.AnyAsync(ct))
        {
            var referrals = members
                .Where(m => m.ReferredByMemberId is not null)
                .Select(m => new Referral
                {
                    ReferrerMemberId = m.ReferredByMemberId!.Value,
                    Code = members.First(x => x.Id == m.ReferredByMemberId).ReferralCode ?? "FORGE",
                    InviteeMemberId = m.Id,
                    InviteeName = m.FullName,
                    InviteePhone = m.Phone,
                    Status = ReferralStatus.Rewarded,
                    ReferrerRewarded = true,
                    InviteeRewarded = true,
                    ConvertedAtUtc = m.JoinedOn.ToDateTime(new TimeOnly(12, 0)).AddMinutes(-330),
                    CreatedAtUtc = m.JoinedOn.ToDateTime(new TimeOnly(9, 0)).AddMinutes(-330)
                })
                .ToList();

            db.Referrals.AddRange(referrals);
            await db.SaveChangesAsync(ct);
        }

        if (!await db.MemberBadges.AnyAsync(ct))
        {
            var badges = await db.Badges.ToListAsync(ct);
            var checkInCounts = await db.CheckIns
                .GroupBy(c => c.MemberId)
                .Select(g => new { MemberId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.MemberId, x => x.Count, ct);

            var awarded = new List<MemberBadge>();
            foreach (var member in members)
            {
                var visits = checkInCounts.GetValueOrDefault(member.Id);
                foreach (var badge in badges)
                {
                    var earned = badge.Slug switch
                    {
                        "first-session" => visits >= 1,
                        "ten-visits" => visits >= 10,
                        "fifty-visits" => visits >= 50,
                        "two-hundred-visits" => visits >= 200,
                        "streak-14" => member.LongestStreakDays >= 14,
                        "streak-100" => member.LongestStreakDays >= 100,
                        _ => false
                    };
                    if (earned)
                        awarded.Add(new MemberBadge
                        {
                            MemberId = member.Id, BadgeId = badge.Id,
                            AwardedAtUtc = DateTime.UtcNow.AddDays(-rng.Next(1, 120)), IsSeen = rng.NextDouble() < 0.7
                        });
                }
            }

            db.MemberBadges.AddRange(awarded);
            await db.SaveChangesAsync(ct);
        }

        if (!await db.FeedPosts.AnyAsync(ct))
        {
            var prLogs = await db.WorkoutLogs
                .Where(l => l.IsPersonalRecord)
                .OrderByDescending(l => l.PerformedOn)
                .Take(14)
                .Include(l => l.Exercise)
                .Include(l => l.Member)
                .ToListAsync(ct);

            var posts = prLogs.Select(l => new FeedPost
            {
                Kind = FeedPostKind.PersonalRecord,
                MemberId = l.MemberId,
                BranchId = l.Member.HomeBranchId,
                Title = $"{l.Member.FullName.Split(' ')[0]} hit a new best on the {l.Exercise.Name.ToLowerInvariant()} — {l.WeightKg:0.#} kg × {l.Reps}",
                MetaJson = $"{{\"exercise\":\"{l.Exercise.Slug}\",\"weightKg\":{l.WeightKg:0.#},\"reps\":{l.Reps},\"e1rm\":{l.EstimatedOneRepMax:0.#}}}",
                LikeCount = rng.Next(3, 48),
                PostedAtUtc = l.PerformedAtUtc.AddMinutes(12),
                CreatedAtUtc = l.PerformedAtUtc.AddMinutes(12)
            }).ToList();

            posts.Add(new FeedPost
            {
                Kind = FeedPostKind.Announcement,
                BranchId = branches[0].Id,
                Title = "New Eleiko platform going in at Koramangala this Sunday",
                Body = "The strength floor loses two racks between 7 AM and 2 PM on Sunday while the fourth platform is installed. Classes run as normal in Studio One.",
                IsPinned = true,
                PostedAtUtc = DateTime.UtcNow.AddDays(-2),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
            });

            db.FeedPosts.AddRange(posts);
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Notifications.AnyAsync(ct))
        {
            var atRisk = members.Where(m => m.ChurnRisk >= ChurnRiskBand.Amber).Take(30).ToList();
            var notifications = atRisk.Select(m => new Notification
            {
                MemberId = m.Id,
                Kind = NotificationKind.WinBack,
                Channel = NotificationChannel.WhatsApp,
                Title = "We have not seen you in a while",
                Body = $"Hi {m.FullName.Split(' ')[0]}, your last session was {(m.LastVisitOn is null ? "a while back" : $"on {m.LastVisitOn:d MMM}")}. Reply BOOK and we will hold a slot with a coach this week.",
                TemplateKey = "winback_v1",
                SentAtUtc = DateTime.UtcNow.AddDays(-rng.Next(0, 5)),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-rng.Next(0, 5))
            }).ToList();

            db.Notifications.AddRange(notifications);
            await db.SaveChangesAsync(ct);
        }
    }

    private static int WeightedFrequency(Random rng) => rng.NextDouble() switch
    {
        < 0.12 => 1,
        < 0.32 => 2,
        < 0.58 => 3,
        < 0.80 => 4,
        < 0.93 => 5,
        _ => 6
    };

    private static int PickVisitHour(DateOnly date, Random rng)
    {
        if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return rng.NextDouble() switch { < 0.45 => rng.Next(7, 11), < 0.75 => rng.Next(11, 17), _ => rng.Next(17, 21) };

        // Twin weekday peaks — 6-8 AM and 6-9 PM.
        return rng.NextDouble() switch
        {
            < 0.34 => rng.Next(5, 9),
            < 0.44 => rng.Next(9, 12),
            < 0.54 => rng.Next(12, 17),
            _ => rng.Next(17, 22)
        };
    }

    private static (int Current, int Longest) Streaks(List<DateOnly> visits, DateOnly today)
    {
        if (visits.Count == 0) return (0, 0);
        var days = visits.Distinct().OrderBy(d => d).ToList();

        var longest = 1;
        var run = 1;
        for (var i = 1; i < days.Count; i++)
        {
            run = days[i].DayNumber - days[i - 1].DayNumber == 1 ? run + 1 : 1;
            longest = Math.Max(longest, run);
        }

        // A streak stays alive if the member trained today or yesterday.
        var last = days[^1];
        if (today.DayNumber - last.DayNumber > 1) return (0, longest);

        var current = 1;
        for (var i = days.Count - 1; i > 0; i--)
        {
            if (days[i].DayNumber - days[i - 1].DayNumber != 1) break;
            current++;
        }
        return (current, Math.Max(longest, current));
    }
}
