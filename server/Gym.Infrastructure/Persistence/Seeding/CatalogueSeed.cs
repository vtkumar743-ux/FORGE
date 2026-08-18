using Gym.Core.Entities;
using Gym.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Gym.Infrastructure.Persistence.Seeding;

/// <summary>Exercise library, retail catalogue, badges and live coupon campaigns.</summary>
internal static class CatalogueSeed
{
    public static async Task<List<Exercise>> ExercisesAsync(GymDbContext db, CancellationToken ct)
    {
        if (await db.Exercises.AnyAsync(ct)) return await db.Exercises.ToListAsync(ct);

        // (name, primary muscle, equipment, level, strength-tracked, cues)
        var rows = new (string Name, MuscleGroup Muscle, EquipmentKind Kit, ClassLevel Level, bool Pr, string Cues)[]
        {
            ("Back Squat", MuscleGroup.Legs, EquipmentKind.Barbell, ClassLevel.Intermediate, true, "Bar on the traps, brace before you unrack, knees track over the mid-foot."),
            ("Front Squat", MuscleGroup.Legs, EquipmentKind.Barbell, ClassLevel.Intermediate, true, "Elbows high, bar sits on the shoulder shelf, torso stays vertical."),
            ("Conventional Deadlift", MuscleGroup.Back, EquipmentKind.Barbell, ClassLevel.Intermediate, true, "Bar over mid-foot, take the slack out, push the floor away."),
            ("Romanian Deadlift", MuscleGroup.Glutes, EquipmentKind.Barbell, ClassLevel.Beginner, true, "Hinge at the hip, bar grazes the thigh, stop when the hamstrings run out."),
            ("Barbell Bench Press", MuscleGroup.Chest, EquipmentKind.Barbell, ClassLevel.Intermediate, true, "Shoulder blades pinned, bar to the sternum, drive the feet."),
            ("Overhead Press", MuscleGroup.Shoulders, EquipmentKind.Barbell, ClassLevel.Intermediate, true, "Ribs down, bar path past the nose, finish with the head through."),
            ("Weighted Pull-Up", MuscleGroup.Back, EquipmentKind.Bodyweight, ClassLevel.Advanced, true, "Full hang, chest to the bar, control the descent."),
            ("Snatch", MuscleGroup.FullBody, EquipmentKind.Barbell, ClassLevel.Advanced, true, "Sweep the bar in, extend fully, punch under fast."),
            ("Clean & Jerk", MuscleGroup.FullBody, EquipmentKind.Barbell, ClassLevel.Advanced, true, "Catch tall in the rack, dip vertical, split under the jerk."),
            ("Barbell Row", MuscleGroup.Back, EquipmentKind.Barbell, ClassLevel.Beginner, true, "Torso at forty-five degrees, bar to the lower ribs, no shrugging."),
            ("Dumbbell Incline Press", MuscleGroup.Chest, EquipmentKind.Dumbbell, ClassLevel.Beginner, false, "Thirty-degree bench, wrists stacked over elbows."),
            ("Lat Pulldown", MuscleGroup.Back, EquipmentKind.Cable, ClassLevel.Beginner, false, "Chest up, pull with the elbows not the hands."),
            ("Seated Cable Row", MuscleGroup.Back, EquipmentKind.Cable, ClassLevel.Beginner, false, "Neutral spine, squeeze for a beat at the ribs."),
            ("Bulgarian Split Squat", MuscleGroup.Legs, EquipmentKind.Dumbbell, ClassLevel.Intermediate, false, "Front shin vertical, back knee tracks down not forward."),
            ("Hip Thrust", MuscleGroup.Glutes, EquipmentKind.Barbell, ClassLevel.Beginner, true, "Chin tucked, ribs down, finish with the hips level."),
            ("Leg Press", MuscleGroup.Legs, EquipmentKind.Machine, ClassLevel.Beginner, false, "Feet shoulder width, stop before the lower back rounds."),
            ("Walking Lunge", MuscleGroup.Legs, EquipmentKind.Dumbbell, ClassLevel.Beginner, false, "Long stride, torso tall, knee kisses the floor."),
            ("Kettlebell Swing", MuscleGroup.Glutes, EquipmentKind.Kettlebell, ClassLevel.Beginner, false, "Hinge not squat, snap the hips, bell floats to chest height."),
            ("Turkish Get-Up", MuscleGroup.FullBody, EquipmentKind.Kettlebell, ClassLevel.Intermediate, false, "Eyes on the bell until standing, one step at a time."),
            ("Farmer's Carry", MuscleGroup.Core, EquipmentKind.Dumbbell, ClassLevel.Beginner, false, "Shoulders packed, ribs down, walk without leaning."),
            ("Plank", MuscleGroup.Core, EquipmentKind.Bodyweight, ClassLevel.Beginner, false, "Squeeze glutes, tuck the ribs, breathe normally."),
            ("Pallof Press", MuscleGroup.Core, EquipmentKind.Cable, ClassLevel.Beginner, false, "Resist the rotation; the press is the easy part."),
            ("Hanging Leg Raise", MuscleGroup.Core, EquipmentKind.Bodyweight, ClassLevel.Intermediate, false, "Posterior tilt first, then lift; no swinging."),
            ("Dead Bug", MuscleGroup.Core, EquipmentKind.Bodyweight, ClassLevel.Beginner, false, "Low back stays flat on the floor throughout."),
            ("Lateral Raise", MuscleGroup.Shoulders, EquipmentKind.Dumbbell, ClassLevel.Beginner, false, "Lead with the elbows, stop at shoulder height."),
            ("Face Pull", MuscleGroup.Shoulders, EquipmentKind.Cable, ClassLevel.Beginner, false, "Rope to the forehead, thumbs back at the finish."),
            ("Barbell Curl", MuscleGroup.Arms, EquipmentKind.Barbell, ClassLevel.Beginner, false, "Elbows pinned to the ribs, no swinging the hips."),
            ("Triceps Rope Extension", MuscleGroup.Arms, EquipmentKind.Cable, ClassLevel.Beginner, false, "Upper arm still, split the rope at the bottom."),
            ("Assault Bike Interval", MuscleGroup.Cardio, EquipmentKind.Cardio, ClassLevel.AllLevels, false, "Sit tall, drive with the legs, hold the target watts."),
            ("Concept2 Row 500m", MuscleGroup.Cardio, EquipmentKind.Cardio, ClassLevel.AllLevels, true, "Legs, back, arms on the drive; reverse on the recovery."),
            ("Ski-Erg Intervals", MuscleGroup.Cardio, EquipmentKind.Cardio, ClassLevel.AllLevels, false, "Hinge and finish past the hips, do not pull with the arms alone."),
            ("Sled Push", MuscleGroup.Legs, EquipmentKind.Machine, ClassLevel.AllLevels, false, "Low body angle, short aggressive steps."),
            ("90/90 Hip Switch", MuscleGroup.Mobility, EquipmentKind.Bodyweight, ClassLevel.Beginner, false, "Move from the hip, keep the chest up and the feet planted."),
            ("Thoracic Bridge", MuscleGroup.Mobility, EquipmentKind.Bodyweight, ClassLevel.Beginner, false, "Reach long through the top arm, breathe into the ribs."),
            ("Banded Ankle Dorsiflexion", MuscleGroup.Mobility, EquipmentKind.Bands, ClassLevel.Beginner, false, "Knee travels over the toe, heel stays down.")
        };

        var exercises = rows.Select(r => new Exercise
        {
            Name = r.Name,
            Slug = Slugify(r.Name),
            PrimaryMuscle = r.Muscle,
            Equipment = r.Kit,
            Level = r.Level,
            IsStrengthTracked = r.Pr,
            Cues = r.Cues,
            ThumbnailUrl = $"/media/exercises/{Slugify(r.Name)}.jpg"
        }).ToList();

        db.Exercises.AddRange(exercises);
        await db.SaveChangesAsync(ct);
        return exercises;
    }

    public static async Task ProductsAsync(GymDbContext db, List<Branch> branches, Random rng, CancellationToken ct)
    {
        if (await db.Products.AnyAsync(ct)) return;

        // GST slabs differ by item — the POS has to build mixed-rate invoices.
        var rows = new (string Name, string Sku, ProductCategory Cat, string Brand, decimal Cost, decimal Sell, decimal Gst, string Hsn)[]
        {
            ("Whey Protein Isolate 1 kg — Chocolate", "SUP-WPI-1K-CHO", ProductCategory.Supplement, "MuscleBlaze", 2450m, 3299m, 18m, "21061000"),
            ("Whey Protein Concentrate 2 kg — Vanilla", "SUP-WPC-2K-VAN", ProductCategory.Supplement, "Optimum Nutrition", 4100m, 5499m, 18m, "21061000"),
            ("Creatine Monohydrate 250 g", "SUP-CRE-250", ProductCategory.Supplement, "MyProtein", 690m, 999m, 18m, "21061000"),
            ("Pre-Workout 300 g — Fruit Punch", "SUP-PRE-300", ProductCategory.Supplement, "Wellcore", 1250m, 1799m, 18m, "21061000"),
            ("Omega-3 Fish Oil 60 caps", "SUP-OM3-60", ProductCategory.Supplement, "HealthKart", 480m, 749m, 12m, "30045020"),
            ("Electrolyte Sachets — Box of 20", "SUP-ELE-20", ProductCategory.Supplement, "Fast&Up", 420m, 649m, 18m, "21069099"),
            ("FORGE Training Tee — Black", "MER-TEE-BLK", ProductCategory.Merchandise, "FORGE", 420m, 899m, 5m, "61091000"),
            ("FORGE Lifting Belt 10 mm", "MER-BLT-10", ProductCategory.Accessory, "FORGE", 1600m, 2499m, 18m, "42033000"),
            ("Wrist Wraps — Pair", "MER-WRP-PR", ProductCategory.Accessory, "FORGE", 340m, 649m, 12m, "63079090"),
            ("Shaker Bottle 700 ml", "MER-SHK-700", ProductCategory.Accessory, "FORGE", 180m, 399m, 18m, "39241090"),
            ("Cold-Pressed Juice 300 ml", "BEV-JUI-300", ProductCategory.Beverage, "Raw Pressery", 75m, 149m, 12m, "20098990"),
            ("Post-Workout Shake — Counter Made", "BEV-SHK-CTR", ProductCategory.Beverage, "FORGE Cafe", 90m, 189m, 5m, "22029990"),
            ("Single Day Pass", "SVC-DAY-01", ProductCategory.DayPass, "FORGE", 0m, 600m, 5m, "999723"),
            ("Guest Pass — 5 Visits", "SVC-GST-05", ProductCategory.DayPass, "FORGE", 0m, 2500m, 5m, "999723")
        };

        var products = rows.Select(r => new Product
        {
            Name = r.Name, Sku = r.Sku, Category = r.Cat, Brand = r.Brand,
            CostPrice = r.Cost, SellPrice = r.Sell, GstRatePercent = r.Gst, HsnCode = r.Hsn,
            Barcode = $"890{Math.Abs(r.Sku.GetHashCode()) % 1000000000:D9}",
            LowStockThreshold = r.Cat == ProductCategory.Supplement ? 6 : 4,
            ImageUrl = $"/media/shop/{r.Sku.ToLowerInvariant()}.jpg",
            Description = null
        }).ToList();

        db.Products.AddRange(products);
        await db.SaveChangesAsync(ct);

        // Physical goods carry stock; services do not. One line is deliberately below
        // threshold so the low-stock alert has something real to show on day one.
        var stock = new List<BranchStock>();
        foreach (var branch in branches)
            foreach (var product in products.Where(p => p.Category is not (ProductCategory.DayPass or ProductCategory.Service)))
            {
                var qty = product.Sku == "SUP-CRE-250" && branch.Slug == "indiranagar" ? 2 : rng.Next(6, 40);
                stock.Add(new BranchStock
                {
                    BranchId = branch.Id, ProductId = product.Id, QuantityOnHand = qty,
                    LastRestockedAtUtc = DateTime.UtcNow.AddDays(-rng.Next(1, 30)),
                    ShelfLocation = product.Category == ProductCategory.Supplement ? "Front counter" : "Retail wall"
                });
            }

        db.BranchStocks.AddRange(stock);
        await db.SaveChangesAsync(ct);
    }

    public static async Task BadgesAsync(GymDbContext db, CancellationToken ct)
    {
        if (await db.Badges.AnyAsync(ct)) return;

        var badges = new List<Badge>
        {
            new() { Name = "First Session", Slug = "first-session", Tier = "bronze", IconKey = "flag", SortOrder = 1,
                Description = "You walked in and did the thing. Hardest session of the lot.",
                CriteriaJson = """{"metric":"checkIns","threshold":1}""" },
            new() { Name = "Ten Visits", Slug = "ten-visits", Tier = "bronze", IconKey = "calendar-check", SortOrder = 2,
                Description = "Ten check-ins logged. This is where it starts feeling normal.",
                CriteriaJson = """{"metric":"checkIns","threshold":10}""" },
            new() { Name = "Half Century", Slug = "fifty-visits", Tier = "silver", IconKey = "medal", SortOrder = 3,
                Description = "Fifty visits on the record.",
                CriteriaJson = """{"metric":"checkIns","threshold":50}""" },
            new() { Name = "Double Century", Slug = "two-hundred-visits", Tier = "gold", IconKey = "trophy", SortOrder = 4,
                Description = "Two hundred sessions. Very few members get here.",
                CriteriaJson = """{"metric":"checkIns","threshold":200}""" },
            new() { Name = "Fortnight Streak", Slug = "streak-14", Tier = "silver", IconKey = "flame", SortOrder = 5,
                Description = "Fourteen consecutive days with a session logged.",
                CriteriaJson = """{"metric":"streakDays","threshold":14}""" },
            new() { Name = "Century Streak", Slug = "streak-100", Tier = "gold", IconKey = "flame", SortOrder = 6,
                Description = "A hundred days unbroken. Put it on the wall.",
                CriteriaJson = """{"metric":"streakDays","threshold":100}""" },
            new() { Name = "Bodyweight Bench", Slug = "bodyweight-bench", Tier = "silver", IconKey = "barbell", SortOrder = 7,
                Description = "Benched your own bodyweight for a clean single.",
                CriteriaJson = """{"metric":"liftRatio","exercise":"barbell-bench-press","threshold":1.0}""" },
            new() { Name = "Two-Plate Squat", Slug = "two-plate-squat", Tier = "gold", IconKey = "barbell", SortOrder = 8,
                Description = "100 kg on the bar, squatted to depth.",
                CriteriaJson = """{"metric":"liftAbsolute","exercise":"back-squat","threshold":100}""" },
            new() { Name = "Class Regular", Slug = "class-regular", Tier = "silver", IconKey = "users", SortOrder = 9,
                Description = "Twenty-five group classes attended.",
                CriteriaJson = """{"metric":"classesAttended","threshold":25}""" },
            new() { Name = "Referral Champion", Slug = "referral-champion", Tier = "gold", IconKey = "share", SortOrder = 10,
                Description = "Three friends joined on your code.",
                CriteriaJson = """{"metric":"referralsConverted","threshold":3}""" }
        };

        db.Badges.AddRange(badges);
        await db.SaveChangesAsync(ct);
    }

    public static async Task CouponsAsync(GymDbContext db, DateOnly today, CancellationToken ct)
    {
        if (await db.Coupons.AnyAsync(ct)) return;

        var coupons = new List<Coupon>
        {
            new()
            {
                Code = "MONSOON25", Name = "Monsoon Reset", DiscountType = DiscountType.Percentage,
                DiscountValue = 25m, MaxDiscountAmount = 5000m, MinOrderAmount = 8000m,
                ValidFrom = today.AddDays(-10), ValidTo = today.AddDays(21), UsageCap = 200, UsageCount = 63,
                PlanScope = "quarterly,half-yearly,annual-all-access",
                Description = "25% off any three-month or longer membership through the monsoon window.",
                ShowAsWebsiteBanner = true,
                BannerHeadline = "Monsoon Reset — 25% off 3-month and longer memberships. Ends in 21 days."
            },
            new()
            {
                Code = "FORGEFRIEND", Name = "Referral Credit", DiscountType = DiscountType.FlatAmount,
                DiscountValue = 500m, MinOrderAmount = 3000m,
                ValidFrom = today.AddYears(-1), ValidTo = today.AddYears(1), PerMemberCap = 5, UsageCount = 118,
                Description = "₹500 off for both sides of a member referral."
            },
            new()
            {
                Code = "CORPWIPRO", Name = "Corporate — Wipro Whitefield", DiscountType = DiscountType.Percentage,
                DiscountValue = 15m, MinOrderAmount = 0m,
                ValidFrom = today.AddMonths(-6), ValidTo = today.AddMonths(6), UsageCount = 41,
                BranchScope = "whitefield",
                Description = "Corporate rate for verified Wipro Whitefield employees enrolling with a company code."
            }
        };

        db.Coupons.AddRange(coupons);
        await db.SaveChangesAsync(ct);
    }

    public static string Slugify(string value)
    {
        var chars = value.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars);
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Trim('-');
    }
}
