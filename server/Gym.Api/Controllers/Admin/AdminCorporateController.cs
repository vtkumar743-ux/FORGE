using System.Text;
using Gym.Core.Entities;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers.Admin;

/// <summary>
/// Corporate accounts (Module 4.6): the company agreements, who has enrolled against them,
/// and the usage report HR asks for at renewal time.
/// </summary>
[ApiController]
[Route("api/admin/corporate")]
[Authorize(Roles = RoleNames.Admin)]
[Produces("application/json")]
public class AdminCorporateController : ControllerBase
{
    private readonly GymDbContext _db;
    private readonly CorporateService _corporate;
    private readonly IClock _clock;

    public AdminCorporateController(GymDbContext db, CorporateService corporate, IClock clock)
    {
        _db = db;
        _corporate = corporate;
        _clock = clock;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CorporateAccountRow>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CorporateAccountRow>>> All(CancellationToken ct)
    {
        var today = _clock.Today;
        var accounts = await _db.CorporateAccounts.AsNoTracking()
            .OrderByDescending(a => a.IsActive).ThenBy(a => a.CompanyName)
            .Select(a => new
            {
                a.Id, a.CompanyName, a.Code, a.Domain, a.HrContactName, a.HrContactEmail, a.HrContactPhone,
                a.DiscountPercent, a.WaiveAdmissionFee, a.SeatCap, a.SeatsUsed, a.BranchScope,
                a.ValidFrom, a.ValidTo, a.IsActive, a.Notes,
                ActiveEnrolments = a.Enrolments.Count(e => e.IsActive)
            })
            .ToListAsync(ct);

        return Ok(accounts.Select(a => new CorporateAccountRow
        {
            Id = a.Id,
            CompanyName = a.CompanyName,
            Code = a.Code,
            Domain = a.Domain,
            HrContactName = a.HrContactName,
            HrContactEmail = a.HrContactEmail,
            HrContactPhone = a.HrContactPhone,
            DiscountPercent = a.DiscountPercent,
            WaiveAdmissionFee = a.WaiveAdmissionFee,
            SeatCap = a.SeatCap,
            SeatsUsed = a.ActiveEnrolments,
            SeatsLeft = a.SeatCap is { } cap ? Math.Max(0, cap - a.ActiveEnrolments) : null,
            BranchScope = a.BranchScope,
            ValidFrom = a.ValidFrom.ToString("yyyy-MM-dd"),
            ValidTo = a.ValidTo.ToString("yyyy-MM-dd"),
            IsActive = a.IsActive,
            Notes = a.Notes,
            Status = !a.IsActive ? "Paused"
                : a.ValidFrom > today ? "Scheduled"
                : a.ValidTo < today ? "Expired"
                : a.SeatCap is { } c && a.ActiveEnrolments >= c ? "Full"
                : "Live"
        }).ToList());
    }

    [HttpPost]
    [ProducesResponseType(typeof(CorporateAccountRow), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(UpsertCorporateRequest request, CancellationToken ct)
    {
        var code = CorporateService.Normalise(request.Code);
        if (code.Length < 3)
            return BadRequest(new ProblemDetails { Title = "A company code needs at least three characters." });
        if (await _db.CorporateAccounts.AnyAsync(a => a.Code == code, ct))
            return Conflict(new ProblemDetails { Title = $"The code {code} is already in use." });

        var account = new CorporateAccount { Code = code, CreatedBy = User.Identity?.Name };
        Apply(account, request);
        _db.CorporateAccounts.Add(account);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(All), new { id = account.Id }, new { account.Id, account.Code });
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, UpsertCorporateRequest request, CancellationToken ct)
    {
        var account = await _db.CorporateAccounts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account is null) return NotFound();

        // The code is what employees have already been given; changing it silently strands them.
        Apply(account, request);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Accounts are retired, never deleted: the enrolment history is what a renewal
    /// conversation is built on, and invoices reference the members it covered.
    /// </summary>
    [HttpPost("{id:int}/retire")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Retire(int id, CancellationToken ct)
    {
        var account = await _db.CorporateAccounts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account is null) return NotFound();

        account.IsActive = false;
        var enrolments = await _db.CorporateEnrolments.Where(e => e.CorporateAccountId == id && e.IsActive).ToListAsync(ct);
        foreach (var enrolment in enrolments)
        {
            enrolment.IsActive = false;
            enrolment.EndedOn = _clock.Today;
        }
        account.SeatsUsed = 0;
        await _db.SaveChangesAsync(ct);

        return Ok(new { retired = true, enrolmentsClosed = enrolments.Count });
    }

    [HttpGet("{id:int}/usage")]
    [ProducesResponseType(typeof(CorporateUsageReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CorporateUsageReport>> Usage(
        int id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var end = to ?? _clock.Today;
        var start = from ?? end.AddDays(-89);
        var report = await _corporate.UsageAsync(id, start, end, ct);
        return report is null ? NotFound() : Ok(report);
    }

    /// <summary>The export HR actually opens. CSV with a BOM so Excel reads Indian names intact.</summary>
    [HttpGet("{id:int}/usage.csv")]
    [Produces("text/csv")]
    public async Task<IActionResult> UsageCsv(
        int id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var end = to ?? _clock.Today;
        var start = from ?? end.AddDays(-89);
        var report = await _corporate.UsageAsync(id, start, end, ct);
        if (report is null) return NotFound();

        var csv = CorporateService.ToCsv(report);
        var fileName = $"{report.Code}-usage-{start:yyyyMMdd}-{end:yyyyMMdd}.csv";
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    [HttpPost("enrolments/{enrolmentId:int}/end")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> EndEnrolment(int enrolmentId, CancellationToken ct)
    {
        var ended = await _corporate.EndEnrolmentAsync(enrolmentId, ct);
        return ended ? Ok(new { ended = true }) : NotFound();
    }

    /// <summary>Desk-side enrolment, for the employee who walks in rather than signing up online.</summary>
    [HttpPost("enrol")]
    [ProducesResponseType(typeof(CorporateCodeResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CorporateCodeResult>> Enrol(AdminEnrolRequest request, CancellationToken ct)
    {
        var result = await _corporate.EnrolAsync(request.MemberId, request.Code, request.EmployeeId, request.WorkEmail, ct);
        return result.Accepted ? Ok(result) : Conflict(result);
    }

    private void Apply(CorporateAccount account, UpsertCorporateRequest request)
    {
        account.CompanyName = request.CompanyName.Trim();
        account.Domain = string.IsNullOrWhiteSpace(request.Domain) ? null : request.Domain.Trim().TrimStart('@');
        account.HrContactName = request.HrContactName.Trim();
        account.HrContactEmail = request.HrContactEmail.Trim();
        account.HrContactPhone = string.IsNullOrWhiteSpace(request.HrContactPhone) ? null : request.HrContactPhone.Trim();
        account.DiscountPercent = Math.Clamp(request.DiscountPercent, 0m, 75m);
        account.WaiveAdmissionFee = request.WaiveAdmissionFee;
        account.SeatCap = request.SeatCap is > 0 ? request.SeatCap : null;
        account.BranchScope = string.IsNullOrWhiteSpace(request.BranchScope) ? null : request.BranchScope.Trim();
        account.ValidFrom = request.ValidFrom ?? _clock.Today;
        account.ValidTo = request.ValidTo ?? _clock.Today.AddYears(1);
        account.IsActive = request.IsActive;
        account.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        account.UpdatedAtUtc = _clock.UtcNow;
        account.UpdatedBy = User.Identity?.Name;
    }
}

public record UpsertCorporateRequest
{
    public string Code { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string? Domain { get; init; }
    public string HrContactName { get; init; } = string.Empty;
    public string HrContactEmail { get; init; } = string.Empty;
    public string? HrContactPhone { get; init; }
    public decimal DiscountPercent { get; init; }
    public bool WaiveAdmissionFee { get; init; } = true;
    public int? SeatCap { get; init; }
    public string? BranchScope { get; init; }
    public DateOnly? ValidFrom { get; init; }
    public DateOnly? ValidTo { get; init; }
    public bool IsActive { get; init; } = true;
    public string? Notes { get; init; }
}

public record AdminEnrolRequest
{
    public required int MemberId { get; init; }
    public required string Code { get; init; }
    public string? EmployeeId { get; init; }
    public string? WorkEmail { get; init; }
}

public record CorporateAccountRow
{
    public required int Id { get; init; }
    public required string CompanyName { get; init; }
    public required string Code { get; init; }
    public string? Domain { get; init; }
    public required string HrContactName { get; init; }
    public required string HrContactEmail { get; init; }
    public string? HrContactPhone { get; init; }
    public decimal DiscountPercent { get; init; }
    public bool WaiveAdmissionFee { get; init; }
    public int? SeatCap { get; init; }
    public int SeatsUsed { get; init; }
    public int? SeatsLeft { get; init; }
    public string? BranchScope { get; init; }
    public required string ValidFrom { get; init; }
    public required string ValidTo { get; init; }
    public bool IsActive { get; init; }
    public string? Notes { get; init; }
    public required string Status { get; init; }
}
