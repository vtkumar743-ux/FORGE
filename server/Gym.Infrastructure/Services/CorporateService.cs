using System.Globalization;
using System.Text;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure.Services;

/// <summary>
/// Corporate memberships (Module 4.6). An employee enrols themselves with a code rather than
/// the desk keying in forty people one at a time, and HR gets a usage export at the end of
/// the quarter — which is the artefact that decides whether the contract renews.
/// </summary>
public class CorporateService
{
    private readonly GymDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<CorporateService> _log;

    public CorporateService(GymDbContext db, IClock clock, ILogger<CorporateService> log)
    {
        _db = db;
        _clock = clock;
        _log = log;
    }

    /// <summary>What a member sees before they commit — the benefit, in words, with no side effects.</summary>
    public async Task<CorporateCodeResult> PreviewAsync(string code, int? branchId, CancellationToken ct = default)
    {
        var normalised = Normalise(code);
        if (normalised.Length == 0) return CorporateCodeResult.Rejected("Enter your company code.");

        var account = await _db.CorporateAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Code == normalised, ct);

        if (account is null) return CorporateCodeResult.Rejected("We do not recognise that company code.");

        var today = _clock.Today;
        if (!account.IsActive) return CorporateCodeResult.Rejected($"The {account.CompanyName} programme is not running at the moment.");
        if (account.ValidFrom > today) return CorporateCodeResult.Rejected($"The {account.CompanyName} programme opens on {account.ValidFrom:dd MMM yyyy}.");
        if (account.ValidTo < today) return CorporateCodeResult.Rejected($"The {account.CompanyName} programme ended on {account.ValidTo:dd MMM yyyy}.");
        if (account.SeatCap is { } cap && account.SeatsUsed >= cap)
            return CorporateCodeResult.Rejected($"Every seat on the {account.CompanyName} programme is taken. Ask your HR team to add more.");

        if (branchId is { } branch && !string.IsNullOrWhiteSpace(account.BranchScope))
        {
            var allowed = account.BranchScope
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => int.TryParse(x, out var id) ? id : -1)
                .ToHashSet();
            if (!allowed.Contains(branch))
                return CorporateCodeResult.Rejected($"The {account.CompanyName} programme does not cover this branch.");
        }

        var seats = account.SeatCap is { } total ? $"{total - account.SeatsUsed} of {total} seats left" : "Unlimited seats";
        return new CorporateCodeResult(true, null, account.Id, account.CompanyName,
            account.DiscountPercent, account.WaiveAdmissionFee, seats, account.ValidTo);
    }

    /// <summary>
    /// Enrols the member. Seats are counted on the account row and the enrolment is its own
    /// record, so someone who leaves the company keeps their training history and the seat
    /// goes back to the pool.
    /// </summary>
    public async Task<CorporateCodeResult> EnrolAsync(
        int memberId, string code, string? employeeId, string? workEmail, CancellationToken ct = default)
    {
        var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == memberId, ct);
        if (member is null) return CorporateCodeResult.Rejected("Member not found.");

        var preview = await PreviewAsync(code, member.HomeBranchId, ct);
        if (!preview.Accepted) return preview;

        var account = await _db.CorporateAccounts.FirstAsync(a => a.Id == preview.AccountId, ct);

        var existing = await _db.CorporateEnrolments
            .FirstOrDefaultAsync(e => e.MemberId == memberId && e.IsActive, ct);
        if (existing is not null)
        {
            if (existing.CorporateAccountId == account.Id)
                return CorporateCodeResult.Rejected($"You are already on the {account.CompanyName} programme.");

            // Changing employer: release the old seat rather than silently double-counting.
            existing.IsActive = false;
            existing.EndedOn = _clock.Today;
            var previous = await _db.CorporateAccounts.FirstOrDefaultAsync(a => a.Id == existing.CorporateAccountId, ct);
            if (previous is not null) previous.SeatsUsed = Math.Max(0, previous.SeatsUsed - 1);
        }

        if (!string.IsNullOrWhiteSpace(account.Domain) && !string.IsNullOrWhiteSpace(workEmail)
            && !workEmail.Trim().EndsWith("@" + account.Domain.Trim().TrimStart('@'), StringComparison.OrdinalIgnoreCase))
            return CorporateCodeResult.Rejected($"Use your @{account.Domain.TrimStart('@')} work address to join this programme.");

        _db.CorporateEnrolments.Add(new CorporateEnrolment
        {
            CorporateAccountId = account.Id,
            MemberId = memberId,
            EmployeeId = employeeId?.Trim(),
            WorkEmail = workEmail?.Trim(),
            EnrolledOn = _clock.Today,
            IsActive = true
        });

        account.SeatsUsed += 1;
        member.CorporateAccountId = account.Id;
        member.CorporateCode = account.Code;

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Member {MemberId} enrolled on corporate account {Code}", memberId, account.Code);

        return preview with { Message = $"You are on the {account.CompanyName} programme — {account.DiscountPercent:0.#}% off every renewal." };
    }

    public async Task<bool> EndEnrolmentAsync(int enrolmentId, CancellationToken ct = default)
    {
        var enrolment = await _db.CorporateEnrolments
            .Include(e => e.CorporateAccount)
            .FirstOrDefaultAsync(e => e.Id == enrolmentId, ct);
        if (enrolment is null || !enrolment.IsActive) return false;

        enrolment.IsActive = false;
        enrolment.EndedOn = _clock.Today;
        enrolment.CorporateAccount.SeatsUsed = Math.Max(0, enrolment.CorporateAccount.SeatsUsed - 1);

        var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == enrolment.MemberId, ct);
        if (member is not null && member.CorporateAccountId == enrolment.CorporateAccountId)
        {
            member.CorporateAccountId = null;
            member.CorporateCode = null;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// The HR report: who is enrolled, whether they actually turn up, and what the company's
    /// people are getting for the money. Usage is the only number that renews a contract.
    /// </summary>
    public async Task<CorporateUsageReport?> UsageAsync(
        int accountId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var account = await _db.CorporateAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, ct);
        if (account is null) return null;

        var fromUtc = from.ToDateTime(TimeOnly.MinValue).AddHours(-5.5);
        var toUtc = to.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-5.5);

        var enrolments = await _db.CorporateEnrolments.AsNoTracking()
            .Where(e => e.CorporateAccountId == accountId)
            .Include(e => e.Member).ThenInclude(m => m.HomeBranch)
            .OrderBy(e => e.Member.FullName)
            .ToListAsync(ct);

        var memberIds = enrolments.Select(e => e.MemberId).ToList();

        var visits = await _db.CheckIns.AsNoTracking()
            .Where(c => memberIds.Contains(c.MemberId) && !c.WasBlocked
                        && c.CheckInAtUtc >= fromUtc && c.CheckInAtUtc < toUtc)
            .GroupBy(c => c.MemberId)
            .Select(g => new { MemberId = g.Key, Visits = g.Count(), Last = g.Max(x => x.CheckInAtUtc) })
            .ToDictionaryAsync(x => x.MemberId, x => x, ct);

        var classes = await _db.Bookings.AsNoTracking()
            .Where(b => memberIds.Contains(b.MemberId) && b.Status == BookingStatus.Attended
                        && b.ClassSession.StartsAtUtc >= fromUtc && b.ClassSession.StartsAtUtc < toUtc)
            .GroupBy(b => b.MemberId)
            .Select(g => new { MemberId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MemberId, x => x.Count, ct);

        var spend = await _db.Invoices.AsNoTracking()
            .Where(i => memberIds.Contains(i.MemberId) && i.IssuedOn >= from && i.IssuedOn <= to)
            .GroupBy(i => i.MemberId)
            .Select(g => new { MemberId = g.Key, Total = g.Sum(x => x.GrandTotal) })
            .ToDictionaryAsync(x => x.MemberId, x => x.Total, ct);

        var activeSubs = await _db.Subscriptions.AsNoTracking()
            .Where(s => memberIds.Contains(s.MemberId) && s.Status == SubscriptionStatus.Active)
            .Select(s => new { s.MemberId, PlanName = s.Plan.Name, s.EndsOn })
            .ToListAsync(ct);
        var planByMember = activeSubs
            .GroupBy(s => s.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.EndsOn).First());

        var rows = enrolments.Select(e =>
        {
            var visit = visits.GetValueOrDefault(e.MemberId);
            var plan = planByMember.GetValueOrDefault(e.MemberId);
            return new CorporateUsageRow
            {
                MemberId = e.MemberId,
                MemberCode = e.Member.MemberCode,
                Name = e.Member.FullName,
                EmployeeId = e.EmployeeId,
                WorkEmail = e.WorkEmail,
                Branch = e.Member.HomeBranch.Name,
                EnrolledOn = e.EnrolledOn,
                EndedOn = e.EndedOn,
                IsActive = e.IsActive,
                Plan = plan?.PlanName,
                PlanEndsOn = plan?.EndsOn,
                Visits = visit?.Visits ?? 0,
                LastVisitOn = visit is null ? null : DateOnly.FromDateTime(visit.Last.AddHours(5.5)),
                ClassesAttended = classes.GetValueOrDefault(e.MemberId),
                AmountInvoiced = spend.GetValueOrDefault(e.MemberId)
            };
        }).ToList();

        return new CorporateUsageReport
        {
            AccountId = account.Id,
            CompanyName = account.CompanyName,
            Code = account.Code,
            From = from,
            To = to,
            SeatCap = account.SeatCap,
            SeatsUsed = account.SeatsUsed,
            DiscountPercent = account.DiscountPercent,
            Rows = rows,
            TotalVisits = rows.Sum(r => r.Visits),
            // "Active" here means actually training, not merely enrolled — the distinction is
            // the entire point of sending HR a usage report.
            ActiveUsers = rows.Count(r => r.Visits > 0),
            NeverVisited = rows.Count(r => r.IsActive && r.Visits == 0),
            TotalInvoiced = rows.Sum(r => r.AmountInvoiced)
        };
    }

    public static string ToCsv(CorporateUsageReport report)
    {
        var sb = new StringBuilder();
        // BOM so Excel opens Indian names intact — the same fix the members export needed.
        sb.Append('﻿');
        sb.AppendLine($"FORGE corporate usage,{Escape(report.CompanyName)},{report.Code}");
        sb.AppendLine($"Period,{report.From:yyyy-MM-dd},{report.To:yyyy-MM-dd}");
        sb.AppendLine($"Seats used,{report.SeatsUsed},{(report.SeatCap?.ToString() ?? "unlimited")}");
        sb.AppendLine($"Active users,{report.ActiveUsers},Never visited,{report.NeverVisited}");
        sb.AppendLine();
        sb.AppendLine("Member code,Name,Employee id,Work email,Branch,Enrolled on,Status,Plan,Plan ends,Visits,Last visit,Classes attended,Invoiced");

        foreach (var r in report.Rows)
            sb.AppendLine(string.Join(',',
                Escape(r.MemberCode), Escape(r.Name), Escape(r.EmployeeId), Escape(r.WorkEmail), Escape(r.Branch),
                r.EnrolledOn.ToString("yyyy-MM-dd"),
                r.IsActive ? "Active" : $"Ended {r.EndedOn:yyyy-MM-dd}",
                Escape(r.Plan), r.PlanEndsOn?.ToString("yyyy-MM-dd") ?? string.Empty,
                r.Visits.ToString(CultureInfo.InvariantCulture),
                r.LastVisitOn?.ToString("yyyy-MM-dd") ?? string.Empty,
                r.ClassesAttended.ToString(CultureInfo.InvariantCulture),
                r.AmountInvoiced.ToString("0.00", CultureInfo.InvariantCulture)));

        return sb.ToString();
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;
    }

    public static string Normalise(string? code) =>
        new string((code ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}

public record CorporateCodeResult(
    bool Accepted, string? Message, int AccountId, string CompanyName,
    decimal DiscountPercent, bool WaiveAdmissionFee, string SeatSummary, DateOnly ValidTo)
{
    public static CorporateCodeResult Rejected(string message) =>
        new(false, message, 0, string.Empty, 0m, false, string.Empty, default);
}

public record CorporateUsageReport
{
    public required int AccountId { get; init; }
    public required string CompanyName { get; init; }
    public required string Code { get; init; }
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public int? SeatCap { get; init; }
    public int SeatsUsed { get; init; }
    public decimal DiscountPercent { get; init; }
    public required IReadOnlyList<CorporateUsageRow> Rows { get; init; }
    public int TotalVisits { get; init; }
    public int ActiveUsers { get; init; }
    public int NeverVisited { get; init; }
    public decimal TotalInvoiced { get; init; }
}

public record CorporateUsageRow
{
    public required int MemberId { get; init; }
    public required string MemberCode { get; init; }
    public required string Name { get; init; }
    public string? EmployeeId { get; init; }
    public string? WorkEmail { get; init; }
    public required string Branch { get; init; }
    public required DateOnly EnrolledOn { get; init; }
    public DateOnly? EndedOn { get; init; }
    public bool IsActive { get; init; }
    public string? Plan { get; init; }
    public DateOnly? PlanEndsOn { get; init; }
    public int Visits { get; init; }
    public DateOnly? LastVisitOn { get; init; }
    public int ClassesAttended { get; init; }
    public decimal AmountInvoiced { get; init; }
}
