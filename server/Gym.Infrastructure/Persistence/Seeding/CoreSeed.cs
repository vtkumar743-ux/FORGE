using Gym.Core.Entities;
using Gym.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Gym.Infrastructure.Persistence.Seeding;

/// <summary>Branches, rooms, plans, trainers, class library, timetable, exercises, retail, badges.</summary>
internal static class CoreSeed
{
    public static async Task<List<Branch>> BranchesAsync(GymDbContext db, CancellationToken ct)
    {
        if (await db.Branches.AnyAsync(ct)) return await db.Branches.OrderBy(b => b.DisplayOrder).ToListAsync(ct);

        var branches = new List<Branch>
        {
            new()
            {
                Name = "FORGE Koramangala", Slug = "koramangala", MemberCodePrefix = "KOR", City = "Bengaluru", State = "Karnataka",
                AddressLine1 = "1042, 80 Feet Road, 5th Block", AddressLine2 = "Above Third Wave Coffee, Koramangala",
                Pincode = "560095", Phone = "+918041729500", WhatsAppNumber = "+919148120500",
                Email = "koramangala@forgestrength.in",
                Latitude = 12.935200m, Longitude = 77.624500m,
                GoogleMapsUrl = "https://maps.google.com/?q=12.9352,77.6245",
                WeekdayOpen = new TimeOnly(5, 0), WeekdayClose = new TimeOnly(23, 0),
                WeekendOpen = new TimeOnly(6, 0), WeekendClose = new TimeOnly(22, 0),
                FloorAreaSqft = 18000, OccupancyCapacity = 180, Gstin = "29AABCF4521K1ZP",
                HeroImageUrl = "/media/branches/koramangala-hero.jpg",
                ShortPitch = "The original floor. Four Eleiko platforms, a 24-station strength deck and the city's busiest 7 PM class block.",
                DisplayOrder = 1
            },
            new()
            {
                Name = "FORGE Indiranagar", Slug = "indiranagar", MemberCodePrefix = "IND", City = "Bengaluru", State = "Karnataka",
                AddressLine1 = "318, 100 Feet Road, HAL 2nd Stage", AddressLine2 = "Opposite Sony Signal, Indiranagar",
                Pincode = "560038", Phone = "+918041729540", WhatsAppNumber = "+919148120540",
                Email = "indiranagar@forgestrength.in",
                Latitude = 12.971900m, Longitude = 77.641200m,
                GoogleMapsUrl = "https://maps.google.com/?q=12.9719,77.6412",
                WeekdayOpen = new TimeOnly(5, 30), WeekdayClose = new TimeOnly(22, 30),
                WeekendOpen = new TimeOnly(6, 30), WeekendClose = new TimeOnly(21, 0),
                FloorAreaSqft = 14000, OccupancyCapacity = 140, Gstin = "29AABCF4521K2ZN",
                HeroImageUrl = "/media/branches/indiranagar-hero.jpg",
                ShortPitch = "Studio-led and daylight-heavy. Two glass-walled studios, a 28-bike spin room and the shortest average class wait in the network.",
                DisplayOrder = 2
            },
            new()
            {
                Name = "FORGE Whitefield", Slug = "whitefield", MemberCodePrefix = "WHF", City = "Bengaluru", State = "Karnataka",
                AddressLine1 = "Ground Floor, Ascendas Park Square Mall", AddressLine2 = "ITPL Main Road, Whitefield",
                Pincode = "560066", Phone = "+918041729580", WhatsAppNumber = "+919148120580",
                Email = "whitefield@forgestrength.in",
                Latitude = 12.985600m, Longitude = 77.735900m,
                GoogleMapsUrl = "https://maps.google.com/?q=12.9856,77.7359",
                WeekdayOpen = new TimeOnly(5, 0), WeekdayClose = new TimeOnly(23, 30),
                WeekendOpen = new TimeOnly(6, 0), WeekendClose = new TimeOnly(22, 0),
                FloorAreaSqft = 22000, OccupancyCapacity = 220, Gstin = "29AABCF4521K3ZL",
                HeroImageUrl = "/media/branches/whitefield-hero.jpg",
                ShortPitch = "Our largest floor. Six platforms, a 60-metre turf lane, ice-bath recovery suite and parking that actually works.",
                DisplayOrder = 3
            }
        };

        db.Branches.AddRange(branches);
        await db.SaveChangesAsync(ct);
        return branches;
    }

    public static async Task<List<Room>> RoomsAsync(GymDbContext db, List<Branch> branches, CancellationToken ct)
    {
        if (await db.Rooms.AnyAsync(ct)) return await db.Rooms.ToListAsync(ct);

        var layouts = new Dictionary<string, (string Name, int Capacity)[]>
        {
            ["koramangala"] = new[] { ("Strength Floor", 40), ("Studio One", 24), ("Spin Room", 24), ("Combat Zone", 18), ("Recovery Suite", 12) },
            ["indiranagar"] = new[] { ("Strength Floor", 30), ("Studio One", 22), ("Studio Two", 18), ("Spin Room", 28) },
            ["whitefield"] = new[] { ("Strength Floor", 48), ("Studio One", 26), ("Spin Room", 30), ("Combat Zone", 20), ("Turf Lane", 16), ("Recovery Suite", 14) }
        };

        var rooms = branches
            .SelectMany(b => layouts[b.Slug].Select(r => new Room { BranchId = b.Id, Name = r.Name, Capacity = r.Capacity }))
            .ToList();

        db.Rooms.AddRange(rooms);
        await db.SaveChangesAsync(ct);
        return rooms;
    }

    public static async Task<List<Plan>> PlansAsync(GymDbContext db, List<Branch> branches, CancellationToken ct)
    {
        if (await db.Plans.AnyAsync(ct)) return await db.Plans.Include(p => p.BranchPrices).ToListAsync(ct);

        var plans = new List<Plan>
        {
            new()
            {
                Name = "Monthly", Slug = "monthly", Kind = PlanKind.Recurring, Cycle = BillingCycle.Monthly,
                AccessScope = AccessScope.HomeBranch, DurationDays = 30, BasePrice = 3200m, AdmissionFee = 1500m,
                Tagline = "Month to month, no lock-in",
                Description = "Full access to your home branch, every class on the timetable, and the recovery suite. Cancel or switch branches whenever you like.",
                FeaturesJson = """
                    ["Unlimited gym floor access at your home branch","Every group class included","Recovery suite & sauna","InBody scan on joining","Member app: booking, QR entry, progress tracking"]
                    """,
                TrustMicrocopy = "Cancel anytime · No contract · Admission fee waived on 3 months+",
                FreezeDaysAllowed = 7, GuestPasses = 1, DisplayOrder = 1
            },
            new()
            {
                Name = "Quarterly", Slug = "quarterly", Kind = PlanKind.FixedTerm, Cycle = BillingCycle.Quarterly,
                AccessScope = AccessScope.HomeBranch, DurationDays = 90, BasePrice = 8400m, AdmissionFee = 0m,
                Tagline = "The habit-forming block",
                Description = "Ninety days is where training stops being a decision and starts being a routine. Includes two coached form sessions with a floor trainer.",
                FeaturesJson = """
                    ["Everything in Monthly","No admission fee","2 coached form-check sessions","Personalised workout program","14 freeze days","2 guest passes"]
                    """,
                TrustMicrocopy = "No joining fee · Pause up to 14 days · Upgrade anytime, we credit the balance",
                FreezeDaysAllowed = 14, GuestPasses = 2, DisplayOrder = 2
            },
            new()
            {
                Name = "Half-Yearly", Slug = "half-yearly", Kind = PlanKind.FixedTerm, Cycle = BillingCycle.HalfYearly,
                AccessScope = AccessScope.HomeBranch, DurationDays = 180, BasePrice = 14900m, AdmissionFee = 0m,
                Tagline = "Six months, one price",
                Description = "For members training three or more times a week. Adds quarterly InBody scans and a diet plan reviewed by our nutrition team.",
                FeaturesJson = """
                    ["Everything in Quarterly","Quarterly InBody scan & report","Nutrition plan with Indian meal templates","30 freeze days","4 guest passes","Priority class booking window"]
                    """,
                TrustMicrocopy = "No joining fee · Pause up to 30 days · 7-day money-back on new memberships",
                FreezeDaysAllowed = 30, GuestPasses = 4, DisplayOrder = 3
            },
            new()
            {
                Name = "Annual All-Access", Slug = "annual-all-access", Kind = PlanKind.FixedTerm, Cycle = BillingCycle.Annual,
                AccessScope = AccessScope.AllBranches, DurationDays = 365, BasePrice = 19900m, AdmissionFee = 0m,
                Tagline = "Every branch, every class, all year",
                Description = "Train at Koramangala on weekdays and Whitefield at the weekend. The lowest effective monthly rate we offer, and the only plan with all-branch access.",
                FeaturesJson = """
                    ["Access to all three branches","Everything in Half-Yearly","4 InBody scans a year","2 personal-training sessions included","45 freeze days","6 guest passes","Locker included"]
                    """,
                TrustMicrocopy = "Works out to ₹1,659 a month · Pause up to 45 days · 7-day money-back",
                FreezeDaysAllowed = 45, GuestPasses = 6, PtSessionCredits = 2, IsMostPopular = true, DisplayOrder = 4
            },
            new()
            {
                Name = "Off-Peak", Slug = "off-peak", Kind = PlanKind.Recurring, Cycle = BillingCycle.Monthly,
                AccessScope = AccessScope.HomeBranch, DurationDays = 30, BasePrice = 1900m, AdmissionFee = 750m,
                Tagline = "10 AM to 4 PM, half the price",
                Description = "The floor is quietest between 10 and 4 — every platform free, no waiting for a rack. Built for people who work from home, night shifts and students.",
                FeaturesJson = """
                    ["Gym floor access 10 AM – 4 PM, Monday to Saturday","Off-peak classes included","Recovery suite access","Member app with live occupancy meter"]
                    """,
                TrustMicrocopy = "Cancel anytime · Upgrade to full access any day, pay only the difference",
                AccessWindowStart = new TimeOnly(10, 0), AccessWindowEnd = new TimeOnly(16, 0),
                FreezeDaysAllowed = 7, DisplayOrder = 5
            },
            new()
            {
                Name = "Personal Training — 12 Sessions", Slug = "pt-12", Kind = PlanKind.PtPack, Cycle = BillingCycle.None,
                AccessScope = AccessScope.AllBranches, DurationDays = 120, BasePrice = 18000m, AdmissionFee = 0m,
                Tagline = "One coach, twelve sessions, a real program",
                Description = "Sixty-minute one-to-one sessions with a certified coach at any branch. Includes programming between sessions and weekly check-ins on the app.",
                FeaturesJson = """
                    ["12 × 60-minute one-to-one sessions","Valid 120 days, any branch","Written program updated every block","Weekly check-in on the app","Form-check video review"]
                    """,
                TrustMicrocopy = "Requires an active membership · Sessions transferable within family",
                PtSessionCredits = 12, ShowOnWebsite = true, DisplayOrder = 6
            }
        };

        db.Plans.AddRange(plans);
        await db.SaveChangesAsync(ct);

        // Metro-core vs suburb overrides — Indiranagar carries a premium, Whitefield a discount.
        var multipliers = new Dictionary<string, decimal> { ["koramangala"] = 1.00m, ["indiranagar"] = 1.08m, ["whitefield"] = 0.92m };
        var prices = new List<PlanBranchPrice>();
        foreach (var plan in plans)
            foreach (var branch in branches)
            {
                var price = decimal.Round(plan.BasePrice * multipliers[branch.Slug] / 100m, 0) * 100m;
                prices.Add(new PlanBranchPrice
                {
                    PlanId = plan.Id, BranchId = branch.Id, Price = price,
                    AdmissionFee = plan.AdmissionFee == 0 ? 0m : plan.AdmissionFee,
                    // PT packs are network-wide, so they are not re-priced per branch.
                    IsAvailable = plan.Kind != PlanKind.PtPack || branch.Slug != "indiranagar"
                });
            }

        db.PlanBranchPrices.AddRange(prices);
        await db.SaveChangesAsync(ct);
        return plans;
    }

    public static async Task<List<ClassFormat>> ClassFormatsAsync(GymDbContext db, CancellationToken ct)
    {
        if (await db.ClassFormats.AnyAsync(ct)) return await db.ClassFormats.OrderBy(c => c.DisplayOrder).ToListAsync(ct);

        var formats = new List<ClassFormat>
        {
            new()
            {
                Name = "Strength Foundations", Slug = "strength-foundations", DefaultDurationMinutes = 45, DefaultCapacity = 16,
                Level = ClassLevel.Beginner, Intensity = ClassIntensity.Moderate, EstimatedCalories = 320, IconKey = "barbell",
                ShortDescription = "Squat, hinge, press, pull — coached to a standard, not to failure.",
                Description = "Four compound lifts, a fixed rep scheme and a coach on the floor correcting every rep. This is the class we put every new member through in their first month.",
                Tags = "strength,beginner,coached", DisplayOrder = 1
            },
            new()
            {
                Name = "Olympic Lifting Lab", Slug = "olympic-lifting-lab", DefaultDurationMinutes = 60, DefaultCapacity = 10,
                Level = ClassLevel.Advanced, Intensity = ClassIntensity.High, EstimatedCalories = 420, IconKey = "barbell",
                ShortDescription = "Snatch and clean & jerk on Eleiko platforms, ten athletes maximum.",
                Description = "Technical work on the two competition lifts, filmed and reviewed in the session. Entry requires a coach sign-off on the front squat and overhead position.",
                Tags = "strength,advanced,technique", DisplayOrder = 2
            },
            new()
            {
                Name = "HIIT 45", Slug = "hiit-45", DefaultDurationMinutes = 45, DefaultCapacity = 24,
                Level = ClassLevel.AllLevels, Intensity = ClassIntensity.High, EstimatedCalories = 520, IconKey = "flame",
                ShortDescription = "Rower, ski-erg, sled, kettlebell. Forty-five minutes, no queueing.",
                Description = "Interval work across four stations with heart-rate targets on the wall display. Scaled options called out for every block, so a first-timer and a marathoner run the same class.",
                Tags = "cardio,conditioning,high-intensity", DisplayOrder = 3
            },
            new()
            {
                Name = "Spin Room", Slug = "spin-room", DefaultDurationMinutes = 45, DefaultCapacity = 28,
                Level = ClassLevel.AllLevels, Intensity = ClassIntensity.High, EstimatedCalories = 480, IconKey = "bike",
                ShortDescription = "Keiser M3i bikes, watts on screen, one red room.",
                Description = "Power-based cycling with your output ranked on the leaderboard if you opt in. Lights down, sound up — the room does half the work.",
                Tags = "cardio,cycling,leaderboard", DisplayOrder = 4
            },
            new()
            {
                Name = "Boxing Fundamentals", Slug = "boxing-fundamentals", DefaultDurationMinutes = 50, DefaultCapacity = 18,
                Level = ClassLevel.Beginner, Intensity = ClassIntensity.High, EstimatedCalories = 450, IconKey = "boxing-glove",
                ShortDescription = "Stance, guard, four punches, pad rounds with a coach.",
                Description = "Technique first: footwork and guard before power. Ends with three pad rounds and a conditioning finisher. Wraps provided, gloves available at the desk.",
                Tags = "combat,conditioning,beginner", DisplayOrder = 5
            },
            new()
            {
                Name = "Mobility & Recovery", Slug = "mobility-recovery", DefaultDurationMinutes = 40, DefaultCapacity = 20,
                Level = ClassLevel.AllLevels, Intensity = ClassIntensity.Low, EstimatedCalories = 140, IconKey = "stretch",
                ShortDescription = "Hips, ankles, thoracic spine — the work that keeps you lifting.",
                Description = "Loaded stretching, banded joint work and breathing drills. Programmed as the day after a heavy session, and the class our desk-bound members rate highest.",
                Tags = "mobility,recovery,low-impact", DisplayOrder = 6
            },
            new()
            {
                Name = "Yoga Flow", Slug = "yoga-flow", DefaultDurationMinutes = 60, DefaultCapacity = 22,
                Level = ClassLevel.AllLevels, Intensity = ClassIntensity.Low, EstimatedCalories = 200, IconKey = "lotus",
                ShortDescription = "Vinyasa, taught properly, in a room that isn't freezing.",
                Description = "A continuous sequence built around breath, with hands-on adjustment offered. Mats, blocks and straps are on the studio wall.",
                Tags = "mobility,mindfulness,studio", DisplayOrder = 7
            },
            new()
            {
                Name = "CrossTrain Circuit", Slug = "crosstrain-circuit", DefaultDurationMinutes = 50, DefaultCapacity = 20,
                Level = ClassLevel.Intermediate, Intensity = ClassIntensity.High, EstimatedCalories = 500, IconKey = "kettlebell",
                ShortDescription = "Barbell, gymnastics and metabolic work in one hour.",
                Description = "A strength piece, a skill piece and a timed workout. Scores logged to the app so you can see the same workout six weeks later and beat it.",
                Tags = "conditioning,strength,scored", DisplayOrder = 8
            },
            new()
            {
                Name = "Core & Conditioning", Slug = "core-conditioning", DefaultDurationMinutes = 30, DefaultCapacity = 24,
                Level = ClassLevel.AllLevels, Intensity = ClassIntensity.Moderate, EstimatedCalories = 240, IconKey = "core",
                ShortDescription = "Thirty minutes of anti-rotation, carries and dead bugs.",
                Description = "Trunk work that transfers to the big lifts — loaded carries, planks under tension and rotational control. Slots neatly after a lunchtime session.",
                Tags = "core,express,lunchtime", DisplayOrder = 9
            },
            new()
            {
                Name = "Powerlifting Club", Slug = "powerlifting-club", DefaultDurationMinutes = 75, DefaultCapacity = 12,
                Level = ClassLevel.Advanced, Intensity = ClassIntensity.High, EstimatedCalories = 380, IconKey = "barbell",
                ShortDescription = "Squat, bench, deadlift on a block. Meet prep welcome.",
                Description = "Percentage-based programming across a twelve-week block with a coach handling openers and attempts. Several members compete in Karnataka state meets out of this group.",
                Tags = "strength,advanced,competition", DisplayOrder = 10
            }
        };

        db.ClassFormats.AddRange(formats);
        await db.SaveChangesAsync(ct);
        return formats;
    }

    public static async Task<List<Trainer>> TrainersAsync(GymDbContext db, List<Branch> branches, CancellationToken ct)
    {
        if (await db.Trainers.AnyAsync(ct)) return await db.Trainers.OrderBy(t => t.DisplayOrder).ToListAsync(ct);

        var byslug = branches.ToDictionary(b => b.Slug, b => b.Id);

        var trainers = new List<Trainer>
        {
            new()
            {
                FullName = "Karthik Reddy", Slug = "karthik-reddy", Gender = Gender.Male, PrimaryBranchId = byslug["koramangala"],
                Headline = "Head of Strength · National-level powerlifter",
                Bio = "Karthik has coached out of Koramangala since the floor opened. He competes at 83 kg, holds a 227.5 kg deadlift, and has taken eleven members to their first Karnataka state meet. His sessions are quiet, precise and unusually patient with beginners.",
                Specialties = "Powerlifting, Strength foundations, Meet prep",
                Certifications = "NSCA-CSCS, IPF Level 2 Coach, K11 Advanced",
                YearsExperience = 11, PerClassRate = 1200m, PerPtSessionRate = 1400m, CommissionPercent = 12m, PtSessionPrice = 1800m,
                AverageRating = 4.9m, RatingCount = 214, PortraitUrl = "/media/trainers/karthik-reddy.jpg",
                InstagramUrl = "https://instagram.com/karthik.lifts", DisplayOrder = 1
            },
            new()
            {
                FullName = "Sneha Iyer", Slug = "sneha-iyer", Gender = Gender.Female, PrimaryBranchId = byslug["koramangala"],
                Headline = "Conditioning lead · Ex-national swimmer",
                Bio = "Sneha built the HIIT 45 format and still teaches the 6 AM slot four days a week. She swam for Karnataka through university, which shows up in how she programmes breathing and pacing. Ask her about heart-rate zones and you will lose twenty minutes.",
                Specialties = "HIIT, Metabolic conditioning, Endurance",
                Certifications = "ACSM-CPT, Precision Nutrition L1, TRX Certified",
                YearsExperience = 8, PerClassRate = 1100m, PerPtSessionRate = 1300m, CommissionPercent = 10m, PtSessionPrice = 1600m,
                AverageRating = 4.8m, RatingCount = 186, PortraitUrl = "/media/trainers/sneha-iyer.jpg",
                InstagramUrl = "https://instagram.com/sneha.conditioning", DisplayOrder = 2
            },
            new()
            {
                FullName = "Imran Sheikh", Slug = "imran-sheikh", Gender = Gender.Male, PrimaryBranchId = byslug["koramangala"],
                Headline = "Boxing coach · 24-fight amateur record",
                Bio = "Imran fought out of a Shivajinagar club for nine years before he started coaching. He teaches guard and footwork before anyone throws a hard punch, which is why his beginner class has a waitlist most weeks.",
                Specialties = "Boxing, Pad work, Fight conditioning",
                Certifications = "Boxing India Level 2, ISSA-CPT, First Aid & CPR",
                YearsExperience = 9, PerClassRate = 1150m, PerPtSessionRate = 1350m, CommissionPercent = 10m, PtSessionPrice = 1700m,
                AverageRating = 4.9m, RatingCount = 141, PortraitUrl = "/media/trainers/imran-sheikh.jpg", DisplayOrder = 3
            },
            new()
            {
                FullName = "Divya Nair", Slug = "divya-nair", Gender = Gender.Female, PrimaryBranchId = byslug["indiranagar"],
                Headline = "Movement & mobility · Physiotherapy background",
                Bio = "Divya practised as a physiotherapist for four years before moving to coaching full time, and most of our post-injury members are routed to her. She runs Mobility & Recovery and handles return-to-training plans across all three branches.",
                Specialties = "Mobility, Post-injury rehab, Pre/post-natal",
                Certifications = "BPT, FMS Level 2, Pre/Post-Natal Coaching (GGS)",
                YearsExperience = 7, PerClassRate = 1200m, PerPtSessionRate = 1500m, CommissionPercent = 12m, PtSessionPrice = 1900m,
                AverageRating = 5.0m, RatingCount = 163, PortraitUrl = "/media/trainers/divya-nair.jpg", DisplayOrder = 4
            },
            new()
            {
                FullName = "Rohan Kulkarni", Slug = "rohan-kulkarni", Gender = Gender.Male, PrimaryBranchId = byslug["indiranagar"],
                Headline = "Spin & cycling · Sub-3 marathon",
                Bio = "Rohan runs the red room at Indiranagar and picks every track in it. He races on the road at the weekend and holds a 2:56 marathon, so the wattage targets he sets are earned rather than guessed.",
                Specialties = "Indoor cycling, Endurance, Race prep",
                Certifications = "Keiser M Series Certified, ACE-CPT, RRCA Run Coach",
                YearsExperience = 6, PerClassRate = 1000m, PerPtSessionRate = 1200m, CommissionPercent = 10m, PtSessionPrice = 1500m,
                AverageRating = 4.7m, RatingCount = 209, PortraitUrl = "/media/trainers/rohan-kulkarni.jpg",
                InstagramUrl = "https://instagram.com/rohan.rides", DisplayOrder = 5
            },
            new()
            {
                FullName = "Aparna Menon", Slug = "aparna-menon", Gender = Gender.Female, PrimaryBranchId = byslug["whitefield"],
                Headline = "Yoga & breathwork · 900-hour Ashtanga",
                Bio = "Aparna trained in Mysuru and teaches a strong, unhurried vinyasa. She keeps the studio at a temperature people can actually breathe in and offers hands-on adjustment only when asked first.",
                Specialties = "Vinyasa, Ashtanga, Breathwork",
                Certifications = "RYT-500, Ashtanga (Mysuru, 900 hrs), Yoga Therapy L1",
                YearsExperience = 12, PerClassRate = 1150m, PerPtSessionRate = 1400m, CommissionPercent = 12m, PtSessionPrice = 1700m,
                AverageRating = 4.9m, RatingCount = 178, PortraitUrl = "/media/trainers/aparna-menon.jpg", DisplayOrder = 6
            },
            new()
            {
                FullName = "Bharath Gowda", Slug = "bharath-gowda", Gender = Gender.Male, PrimaryBranchId = byslug["whitefield"],
                Headline = "Olympic lifting · Karnataka state medallist",
                Bio = "Bharath medalled at state level in the 96 kg class and now coaches the lifting lab on the Whitefield platforms. He films every heavy attempt and will make you watch it back before you load the bar again.",
                Specialties = "Olympic weightlifting, Explosive power, Technique",
                Certifications = "IWF Level 1, NSCA-CSCS, Weightlifting India Coach",
                YearsExperience = 10, PerClassRate = 1250m, PerPtSessionRate = 1500m, CommissionPercent = 12m, PtSessionPrice = 1900m,
                AverageRating = 4.8m, RatingCount = 97, PortraitUrl = "/media/trainers/bharath-gowda.jpg", DisplayOrder = 7
            },
            new()
            {
                FullName = "Priya Deshpande", Slug = "priya-deshpande", Gender = Gender.Female, PrimaryBranchId = byslug["whitefield"],
                Headline = "Fat loss & nutrition · Precision Nutrition L2",
                Bio = "Priya handles most of our body-composition work and writes the Indian meal templates every diet plan starts from. She is blunt about what a twelve-week timeline can and cannot do, which members tend to thank her for later.",
                Specialties = "Body recomposition, Nutrition coaching, Habit change",
                Certifications = "Precision Nutrition L2, ISSA-CPT, InBody Certified Specialist",
                YearsExperience = 8, PerClassRate = 1100m, PerPtSessionRate = 1400m, CommissionPercent = 14m, PtSessionPrice = 1800m,
                AverageRating = 4.9m, RatingCount = 152, PortraitUrl = "/media/trainers/priya-deshpande.jpg", DisplayOrder = 8
            }
        };

        db.Trainers.AddRange(trainers);
        await db.SaveChangesAsync(ct);
        return trainers;
    }
}

