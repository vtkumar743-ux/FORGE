using Gym.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.Persistence.Seeding;

public record SeedOptions
{
    /// <summary>Seeded owner credential (04 §4). Overridable from configuration.</summary>
    public string AdminEmail { get; init; } = "admin@gym.local";
    public string? AdminPassword { get; init; }
    public string AdminFullName { get; init; } = "Venkat — Owner";

    /// <summary>How much attendance/booking history to fabricate behind today.</summary>
    public int HistoryDays { get; init; } = 120;
    /// <summary>How far ahead to materialise bookable class sessions.</summary>
    public int FutureDays { get; init; } = 21;
    /// <summary>Fixed so a re-seeded database is byte-for-byte comparable.</summary>
    public int RandomSeed { get; init; } = 20260817;
}

/// <summary>
/// Idempotent, deterministic demo seeder. Every step short-circuits if its table already has
/// rows, so it is safe to run on every startup; the fixed RNG seed means two fresh databases
/// come out identical.
/// </summary>
public class DbSeeder
{
    private readonly GymDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly ILogger<DbSeeder> _log;

    public DbSeeder(
        GymDbContext db,
        UserManager<ApplicationUser> users,
        RoleManager<ApplicationRole> roles,
        ILogger<DbSeeder> log)
    {
        _db = db;
        _users = users;
        _roles = roles;
        _log = log;
    }

    public async Task<SeedResult> RunAsync(SeedOptions options, CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        // IST — the whole business runs on one timezone, and India has no DST.
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(330));
        var rng = new Random(options.RandomSeed);

        await RolesAsync(ct);
        var admin = await AdminAsync(options, ct);

        var branches = await CoreSeed.BranchesAsync(_db, ct);
        var rooms = await CoreSeed.RoomsAsync(_db, branches, ct);
        var plans = await CoreSeed.PlansAsync(_db, branches, ct);
        var formats = await CoreSeed.ClassFormatsAsync(_db, ct);
        var trainers = await CoreSeed.TrainersAsync(_db, branches, ct);

        await CatalogueSeed.CouponsAsync(_db, today, ct);
        var exercises = await CatalogueSeed.ExercisesAsync(_db, ct);
        await CatalogueSeed.ProductsAsync(_db, branches, rng, ct);
        await CatalogueSeed.BadgesAsync(_db, ct);

        var schedules = await TimetableSeed.SchedulesAsync(_db, branches, rooms, formats, trainers, today, ct);
        var sessions = await TimetableSeed.SessionsAsync(_db, schedules, today, 60, options.FutureDays, ct);

        var members = await MemberSeed.SeedAsync(_db, _users, branches, plans, today, rng, ct);

        await ActivitySeed.CheckInsAndStreaksAsync(_db, members, today, options.HistoryDays, rng, ct);
        await ActivitySeed.BookingsAsync(_db, members, sessions, today, rng, ct);
        await ActivitySeed.TrainingHistoryAsync(_db, members, exercises, trainers, today, rng, ct);
        await ActivitySeed.CrmAndEngagementAsync(_db, branches, members, plans, today, rng, ct);
        await ProgramSeed.SeedAsync(_db, members, trainers, today, rng, ct);
        await CorporateSeed.SeedAsync(_db, branches, today, rng, ct);

        await CmsSeed.SeedAsync(_db, branches, today, ct);

        var result = new SeedResult
        {
            AdminEmail = admin.Email,
            AdminPassword = admin.Password,
            AdminWasCreated = admin.WasCreated,
            MemberDemoPassword = MemberSeed.DemoPassword,
            Branches = await _db.Branches.CountAsync(ct),
            Trainers = await _db.Trainers.CountAsync(ct),
            Plans = await _db.Plans.CountAsync(ct),
            WeeklyClasses = await _db.ClassSchedules.CountAsync(ct),
            Sessions = await _db.ClassSessions.CountAsync(ct),
            Members = await _db.Members.CountAsync(ct),
            Bookings = await _db.Bookings.CountAsync(ct),
            CheckIns = await _db.CheckIns.CountAsync(ct),
            Invoices = await _db.Invoices.CountAsync(ct),
            Payments = await _db.Payments.CountAsync(ct),
            Leads = await _db.Leads.CountAsync(ct),
            CmsPages = await _db.CmsPages.CountAsync(ct),
            CmsSections = await _db.CmsSections.CountAsync(ct),
            Programs = await _db.WorkoutPrograms.CountAsync(ct),
            Elapsed = DateTime.UtcNow - started
        };

        _log.LogInformation(
            "Seed complete in {Elapsed:0.0}s — {Branches} branches, {Trainers} trainers, {Plans} plans, " +
            "{WeeklyClasses} weekly classes ({Sessions} sessions), {Members} members, {Bookings} bookings, " +
            "{CheckIns} check-ins, {Invoices} invoices, {Leads} leads, {CmsPages} CMS pages / {CmsSections} sections, " +
            "{Programs} training programmes.",
            result.Elapsed.TotalSeconds, result.Branches, result.Trainers, result.Plans, result.WeeklyClasses,
            result.Sessions, result.Members, result.Bookings, result.CheckIns, result.Invoices, result.Leads,
            result.CmsPages, result.CmsSections, result.Programs);

        return result;
    }

    private async Task RolesAsync(CancellationToken ct)
    {
        foreach (var (name, description) in RoleNames.All)
        {
            if (await _roles.RoleExistsAsync(name)) continue;
            var created = await _roles.CreateAsync(new ApplicationRole { Name = name, Description = description });
            if (!created.Succeeded)
                throw new InvalidOperationException(
                    $"Could not create role '{name}': {string.Join("; ", created.Errors.Select(e => e.Description))}");
        }
    }

    private async Task<(string Email, string Password, bool WasCreated)> AdminAsync(SeedOptions options, CancellationToken ct)
    {
        var existing = await _users.FindByEmailAsync(options.AdminEmail);
        if (existing is not null)
            return (options.AdminEmail, "(unchanged — set previously)", false);

        // Configured password wins; otherwise generate one and print it exactly once.
        var password = string.IsNullOrWhiteSpace(options.AdminPassword)
            ? GeneratePassword()
            : options.AdminPassword;

        var admin = new ApplicationUser
        {
            UserName = options.AdminEmail,
            Email = options.AdminEmail,
            EmailConfirmed = true,
            FullName = options.AdminFullName,
            // Forced on first login per 04 §4; login still succeeds so the flag can be acted on.
            MustChangePassword = true,
            IsActive = true
        };

        var created = await _users.CreateAsync(admin, password);
        if (!created.Succeeded)
            throw new InvalidOperationException(
                $"Could not create the seeded admin: {string.Join("; ", created.Errors.Select(e => e.Description))}");

        await _users.AddToRoleAsync(admin, RoleNames.Admin);
        return (options.AdminEmail, password, true);
    }

    private static string GeneratePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%&*?";
        var all = upper + lower + digits + symbols;

        var chars = new List<char>
        {
            Pick(upper), Pick(lower), Pick(digits), Pick(symbols)
        };
        while (chars.Count < 18) chars.Add(Pick(all));

        // Fisher-Yates with a cryptographic source so the printed password is not predictable.
        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = System.Security.Cryptography.RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars.ToArray());

        static char Pick(string set) => set[System.Security.Cryptography.RandomNumberGenerator.GetInt32(set.Length)];
    }
}

public record SeedResult
{
    public required string AdminEmail { get; init; }
    public required string AdminPassword { get; init; }
    public bool AdminWasCreated { get; init; }
    public required string MemberDemoPassword { get; init; }
    public int Branches { get; init; }
    public int Trainers { get; init; }
    public int Plans { get; init; }
    public int WeeklyClasses { get; init; }
    public int Sessions { get; init; }
    public int Members { get; init; }
    public int Bookings { get; init; }
    public int CheckIns { get; init; }
    public int Invoices { get; init; }
    public int Payments { get; init; }
    public int Leads { get; init; }
    public int CmsPages { get; init; }
    public int CmsSections { get; init; }
    public int Programs { get; init; }
    public TimeSpan Elapsed { get; init; }
}
