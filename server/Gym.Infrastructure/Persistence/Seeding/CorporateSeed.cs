using Gym.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gym.Infrastructure.Persistence.Seeding;

/// <summary>
/// Two corporate agreements with employees already on them (Module 4.6), so the HR usage
/// report has something to say the first time it is opened. Idempotent like every other seed:
/// it short-circuits the moment an account exists.
/// </summary>
public static class CorporateSeed
{
    public static async Task<int> SeedAsync(
        GymDbContext db, IReadOnlyList<Branch> branches, DateOnly today, Random rng, CancellationToken ct = default)
    {
        if (await db.CorporateAccounts.AnyAsync(ct)) return 0;

        var whitefield = branches.FirstOrDefault(b => b.Slug.Contains("whitefield")) ?? branches[0];

        var accounts = new List<CorporateAccount>
        {
            new()
            {
                CompanyName = "Meridian Systems India",
                Code = "MERIDIAN26",
                Domain = "meridiansys.in",
                HrContactName = "Ananya Rao",
                HrContactEmail = "ananya.rao@meridiansys.in",
                HrContactPhone = "9845012233",
                DiscountPercent = 20m,
                WaiveAdmissionFee = true,
                SeatCap = 60,
                // Scoped to the campus branch: the agreement was signed for the office next door.
                BranchScope = whitefield.Id.ToString(),
                ValidFrom = today.AddMonths(-4),
                ValidTo = today.AddMonths(8),
                IsActive = true,
                Notes = "Renewal review each April. HR asks for a quarterly usage export."
            },
            new()
            {
                CompanyName = "Kaveri Health Labs",
                Code = "KAVERI25",
                Domain = "kaverihealth.com",
                HrContactName = "Vikram Menon",
                HrContactEmail = "vikram.menon@kaverihealth.com",
                HrContactPhone = "9880144556",
                DiscountPercent = 15m,
                WaiveAdmissionFee = true,
                SeatCap = null,
                BranchScope = null,
                ValidFrom = today.AddMonths(-1),
                ValidTo = today.AddMonths(11),
                IsActive = true,
                Notes = "All branches. Uncapped seats — they pay per employee who joins."
            }
        };

        db.CorporateAccounts.AddRange(accounts);
        await db.SaveChangesAsync(ct);

        // Enrol a slice of the existing membership so the report is not an empty table. Members
        // are picked from the branches each account actually covers.
        var enrolments = new List<CorporateEnrolment>();
        foreach (var account in accounts)
        {
            var query = db.Members.AsQueryable();
            if (!string.IsNullOrWhiteSpace(account.BranchScope))
            {
                var branchId = int.Parse(account.BranchScope);
                query = query.Where(m => m.HomeBranchId == branchId);
            }

            var candidates = await query
                .Where(m => m.CorporateAccountId == null)
                .OrderBy(m => m.Id)
                .Take(account.Code == "MERIDIAN26" ? 22 : 11)
                .ToListAsync(ct);

            var index = 1;
            foreach (var member in candidates)
            {
                var enrolledOn = account.ValidFrom.AddDays(rng.Next(0, 45));
                if (enrolledOn > today) enrolledOn = today;

                enrolments.Add(new CorporateEnrolment
                {
                    CorporateAccountId = account.Id,
                    MemberId = member.Id,
                    EmployeeId = $"{account.Code[..3]}{index:D4}",
                    WorkEmail = $"{member.FullName.Split(' ')[0].ToLowerInvariant()}.{index}@{account.Domain}",
                    EnrolledOn = enrolledOn,
                    IsActive = true
                });

                member.CorporateAccountId = account.Id;
                member.CorporateCode = account.Code;
                index++;
            }

            account.SeatsUsed = candidates.Count;
        }

        db.CorporateEnrolments.AddRange(enrolments);
        await db.SaveChangesAsync(ct);

        return accounts.Count;
    }
}
