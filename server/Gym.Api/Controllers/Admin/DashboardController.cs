using Gym.Api.Contracts;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers.Admin;

/// <summary>
/// The network overview the owner opens the day on: what the book is worth, who is on the
/// floor, what is owed, who is about to lapse and who is drifting away. Everything is a live
/// query — there is no nightly rollup to go stale.
/// </summary>
[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = RoleNames.Admin)]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly GymDbContext _db;
    private readonly IClock _clock;

    public DashboardController(GymDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    [HttpGet]
    [ProducesResponseType(typeof(DashboardResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardResponse>> Get(
        [FromQuery] int? branchId, [FromQuery] int days = 30, CancellationToken ct = default)
    {
        var today = _clock.Today;
        var span = Math.Clamp(days, 7, 180);
        var windowStart = today.AddDays(-(span - 1));
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var lastMonthStart = monthStart.AddMonths(-1);

        var branches = await _db.Branches.AsNoTracking()
            .Where(b => b.IsActive && (branchId == null || b.Id == branchId))
            .OrderBy(b => b.DisplayOrder)
            .Select(b => new { b.Id, b.Name, b.Slug, b.OccupancyCapacity })
            .ToListAsync(ct);
        var branchIds = branches.Select(b => b.Id).ToList();

        // ---- memberships -------------------------------------------------

        var liveSubs = await _db.Subscriptions.AsNoTracking()
            .Where(s => branchIds.Contains(s.BranchId))
            .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Frozen)
            .Select(s => new
            {
                s.Id, s.BranchId, s.MemberId, s.PriceCharged, s.EndsOn, s.AutoRenew,
                s.Plan.DurationDays, PlanName = s.Plan.Name, s.Status
            })
            .ToListAsync(ct);

        // MRR normalises every term to a month so an annual plan and a monthly one compare.
        decimal Monthly(decimal price, int durationDays) =>
            durationDays <= 0 ? 0 : decimal.Round(price * 30m / durationDays, 2);

        var mrr = decimal.Round(liveSubs.Sum(s => Monthly(s.PriceCharged, s.DurationDays)), 0);

        var mrrLastMonth = decimal.Round(await _db.Subscriptions.AsNoTracking()
            .Where(s => branchIds.Contains(s.BranchId))
            .Where(s => s.StartsOn <= lastMonthStart.AddMonths(1).AddDays(-1) && s.EndsOn >= lastMonthStart)
            .SumAsync(s => s.Plan.DurationDays <= 0 ? 0 : s.PriceCharged * 30m / s.Plan.DurationDays, ct), 0);

        var activeMembers = await _db.Members.AsNoTracking()
            .CountAsync(m => branchIds.Contains(m.HomeBranchId) && m.Status == MemberStatus.Active, ct);

        var activeMembersLastMonth = await _db.Subscriptions.AsNoTracking()
            .Where(s => branchIds.Contains(s.BranchId))
            .Where(s => s.StartsOn <= lastMonthStart.AddMonths(1).AddDays(-1) && s.EndsOn >= lastMonthStart)
            .Select(s => s.MemberId).Distinct().CountAsync(ct);

        // ---- attendance --------------------------------------------------

        var checkInsToday = await _db.CheckIns.AsNoTracking()
            .CountAsync(c => branchIds.Contains(c.BranchId) && c.VisitDate == today && !c.WasBlocked, ct);
        var checkInsYesterday = await _db.CheckIns.AsNoTracking()
            .CountAsync(c => branchIds.Contains(c.BranchId) && c.VisitDate == today.AddDays(-1) && !c.WasBlocked, ct);

        var onFloorByBranch = await _db.CheckIns.AsNoTracking()
            .Where(c => branchIds.Contains(c.BranchId) && c.CheckOutAtUtc == null && !c.WasBlocked)
            .GroupBy(c => c.BranchId)
            .Select(g => new { BranchId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BranchId, x => x.Count, ct);

        // ---- money -------------------------------------------------------

        var dues = await _db.Invoices.AsNoTracking()
            .Where(i => branchIds.Contains(i.BranchId) && i.AmountDue > 0)
            .Where(i => i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Refunded)
            .GroupBy(i => i.BranchId)
            .Select(g => new { BranchId = g.Key, Amount = g.Sum(i => i.AmountDue), Count = g.Count() })
            .ToListAsync(ct);

        var paymentsWindow = await _db.Payments.AsNoTracking()
            .Where(p => branchIds.Contains(p.BranchId) && p.Status == PaymentStatus.Captured)
            .Where(p => p.PaidAtUtc >= lastMonthStart.ToDateTime(TimeOnly.MinValue).AddMinutes(-330))
            .Select(p => new { p.BranchId, p.Amount, p.PaidAtUtc })
            .ToListAsync(ct);

        // Payments are stamped in UTC; the owner reads them on an IST calendar.
        static DateOnly IstDate(DateTime utc) => DateOnly.FromDateTime(utc.AddMinutes(330));

        var revenueThisMonth = paymentsWindow.Where(p => IstDate(p.PaidAtUtc) >= monthStart).Sum(p => p.Amount);
        var revenueLastMonth = paymentsWindow
            .Where(p => IstDate(p.PaidAtUtc) >= lastMonthStart && IstDate(p.PaidAtUtc) < monthStart)
            .Sum(p => p.Amount);

        // ---- series ------------------------------------------------------

        var seriesPayments = await _db.Payments.AsNoTracking()
            .Where(p => branchIds.Contains(p.BranchId) && p.Status == PaymentStatus.Captured)
            .Where(p => p.PaidAtUtc >= windowStart.ToDateTime(TimeOnly.MinValue).AddMinutes(-330))
            .Select(p => new { p.Amount, p.PaidAtUtc })
            .ToListAsync(ct);

        var seriesVisits = await _db.CheckIns.AsNoTracking()
            .Where(c => branchIds.Contains(c.BranchId) && c.VisitDate >= windowStart && !c.WasBlocked)
            .GroupBy(c => c.VisitDate)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count, ct);

        var seriesJoins = await _db.Subscriptions.AsNoTracking()
            .Where(s => branchIds.Contains(s.BranchId) && s.StartsOn >= windowStart)
            .GroupBy(s => s.StartsOn)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count, ct);

        var revenueByDay = seriesPayments
            .GroupBy(p => IstDate(p.PaidAtUtc))
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        var revenue = new List<TimeSeriesPoint>();
        var footfall = new List<TimeSeriesPoint>();
        var joins = new List<TimeSeriesPoint>();
        for (var date = windowStart; date <= today; date = date.AddDays(1))
        {
            var label = date.ToString("dd MMM");
            var iso = date.ToString("yyyy-MM-dd");
            revenue.Add(new TimeSeriesPoint { Label = label, Date = iso, Value = revenueByDay.GetValueOrDefault(date) });
            footfall.Add(new TimeSeriesPoint { Label = label, Date = iso, Value = seriesVisits.GetValueOrDefault(date) });
            joins.Add(new TimeSeriesPoint { Label = label, Date = iso, Value = seriesJoins.GetValueOrDefault(date) });
        }

        // ---- class fill --------------------------------------------------

        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var weekEnd = weekStart.AddDays(6);
        var weekSessions = await _db.ClassSessions.AsNoTracking()
            .Where(s => branchIds.Contains(s.BranchId) && s.SessionDate >= weekStart && s.SessionDate <= weekEnd)
            .Where(s => s.Status != SessionStatus.Cancelled)
            .Select(s => new { s.BranchId, s.Capacity, s.BookedCount })
            .ToListAsync(ct);

        // ---- churn radar and renewals -------------------------------------

        var duesByMember = await _db.Invoices.AsNoTracking()
            .Where(i => i.AmountDue > 0 && i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Refunded)
            .GroupBy(i => i.MemberId)
            .Select(g => new { MemberId = g.Key, Amount = g.Sum(i => i.AmountDue) })
            .ToDictionaryAsync(x => x.MemberId, x => x.Amount, ct);

        var churn = await _db.Members.AsNoTracking()
            .Where(m => branchIds.Contains(m.HomeBranchId))
            .Where(m => m.Status == MemberStatus.Active || m.Status == MemberStatus.Frozen)
            .Where(m => m.ChurnRisk == ChurnRiskBand.Amber || m.ChurnRisk == ChurnRiskBand.Red)
            .OrderByDescending(m => m.ChurnScore).ThenBy(m => m.LastVisitOn)
            .Take(12)
            .Select(m => new
            {
                m.Id, m.MemberCode, m.FullName, m.Phone, BranchName = m.HomeBranch.Name,
                m.ChurnRisk, m.ChurnScore, m.LastVisitOn
            })
            .ToListAsync(ct);

        var subByMember = liveSubs
            .GroupBy(s => s.MemberId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.EndsOn).First());

        var expiringWindow = today.AddDays(7);
        var expiring = await _db.Subscriptions.AsNoTracking()
            .Where(s => branchIds.Contains(s.BranchId) && s.Status == SubscriptionStatus.Active)
            .Where(s => s.EndsOn >= today && s.EndsOn <= expiringWindow)
            .OrderBy(s => s.EndsOn)
            .Take(20)
            .Select(s => new ExpiringRow
            {
                SubscriptionId = s.Id,
                MemberId = s.MemberId,
                MemberCode = s.Member.MemberCode,
                FullName = s.Member.FullName,
                Phone = s.Member.Phone,
                BranchName = s.Branch.Name,
                PlanName = s.Plan.Name,
                EndsOn = s.EndsOn.ToString("yyyy-MM-dd"),
                DaysLeft = s.EndsOn.DayNumber - today.DayNumber,
                PriceCharged = s.PriceCharged,
                AutoRenew = s.AutoRenew
            })
            .ToListAsync(ct);

        // ---- leads -------------------------------------------------------

        var weekAgo = _clock.UtcNow.AddDays(-7);
        var newLeads = await _db.Leads.AsNoTracking()
            .CountAsync(l => l.CreatedAtUtc >= weekAgo && (branchId == null || l.BranchId == branchId), ct);
        var awaiting = await _db.Leads.AsNoTracking()
            .CountAsync(l => l.FirstResponseAtUtc == null && l.Stage != LeadStage.Joined && l.Stage != LeadStage.Lost
                          && (branchId == null || l.BranchId == branchId), ct);

        var recentLeads = await _db.Leads.AsNoTracking()
            .Where(l => l.Stage != LeadStage.Joined && l.Stage != LeadStage.Lost)
            .Where(l => branchId == null || l.BranchId == branchId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(8)
            .Select(l => new LeadCard
            {
                Id = l.Id,
                Reference = "FRG-L" + l.Id.ToString("D5"),
                FullName = l.FullName,
                Phone = l.Phone,
                Email = l.Email,
                BranchId = l.BranchId,
                BranchName = l.Branch != null ? l.Branch.Name : null,
                Stage = l.Stage,
                Source = l.Source,
                SourceDetail = l.SourceDetail,
                Goal = l.Goal,
                InterestedPlanName = l.InterestedPlan != null ? l.InterestedPlan.Name : null,
                TrialRequestedFor = l.TrialRequestedFor != null ? l.TrialRequestedFor.Value.ToString("yyyy-MM-dd") : null,
                AssignedTo = l.AssignedTo,
                CreatedAtUtc = l.CreatedAtUtc,
                FirstResponseAtUtc = l.FirstResponseAtUtc,
                NextFollowUpAtUtc = l.NextFollowUpAtUtc,
                IsOverdue = l.NextFollowUpAtUtc != null && l.NextFollowUpAtUtc < _clock.UtcNow,
                AgeDays = 0,
                OpenFollowUps = l.FollowUps.Count(f => f.CompletedAtUtc == null),
                ConvertedMemberId = l.ConvertedMemberId,
                LostReason = l.LostReason
            })
            .ToListAsync(ct);

        // ---- assemble ------------------------------------------------------

        var branchRows = branches.Select(b =>
        {
            var subs = liveSubs.Where(s => s.BranchId == b.Id).ToList();
            var sessions = weekSessions.Where(s => s.BranchId == b.Id).ToList();
            var capacity = sessions.Sum(s => s.Capacity);
            return new BranchComparison
            {
                BranchId = b.Id,
                Name = b.Name,
                Slug = b.Slug,
                ActiveMembers = subs.Select(s => s.MemberId).Distinct().Count(),
                Mrr = decimal.Round(subs.Sum(s => Monthly(s.PriceCharged, s.DurationDays)), 0),
                CheckInsToday = 0,
                OnFloorNow = onFloorByBranch.GetValueOrDefault(b.Id),
                Capacity = b.OccupancyCapacity,
                DuesOutstanding = dues.FirstOrDefault(d => d.BranchId == b.Id)?.Amount ?? 0m,
                RevenueThisMonth = paymentsWindow
                    .Where(p => p.BranchId == b.Id && IstDate(p.PaidAtUtc) >= monthStart).Sum(p => p.Amount),
                ClassFillPercent = capacity == 0 ? 0 : (int)Math.Round(sessions.Sum(s => s.BookedCount) * 100d / capacity)
            };
        }).ToList();

        var todayByBranch = await _db.CheckIns.AsNoTracking()
            .Where(c => branchIds.Contains(c.BranchId) && c.VisitDate == today && !c.WasBlocked)
            .GroupBy(c => c.BranchId)
            .Select(g => new { BranchId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BranchId, x => x.Count, ct);
        branchRows = branchRows
            .Select(b => b with { CheckInsToday = todayByBranch.GetValueOrDefault(b.BranchId) })
            .ToList();

        var planMix = liveSubs
            .GroupBy(s => s.PlanName)
            .Select(g => new PlanMixRow
            {
                PlanName = g.Key,
                Subscriptions = g.Count(),
                Mrr = decimal.Round(g.Sum(s => Monthly(s.PriceCharged, s.DurationDays)), 0)
            })
            .OrderByDescending(p => p.Mrr)
            .ToList();

        return Ok(new DashboardResponse
        {
            Kpis = new DashboardKpis
            {
                ActiveMembers = activeMembers,
                ActiveMembersLastMonth = activeMembersLastMonth,
                Mrr = mrr,
                MrrLastMonth = mrrLastMonth,
                CheckInsToday = checkInsToday,
                CheckInsYesterday = checkInsYesterday,
                DuesOutstanding = decimal.Round(dues.Sum(d => d.Amount), 0),
                DuesInvoiceCount = dues.Sum(d => d.Count),
                ExpiringInSevenDays = await _db.Subscriptions.AsNoTracking().CountAsync(
                    s => branchIds.Contains(s.BranchId) && s.Status == SubscriptionStatus.Active
                      && s.EndsOn >= today && s.EndsOn <= expiringWindow, ct),
                NewLeadsThisWeek = newLeads,
                LeadsAwaitingFirstResponse = awaiting,
                OnFloorNow = onFloorByBranch.Values.Sum(),
                RevenueThisMonth = decimal.Round(revenueThisMonth, 0),
                RevenueLastMonth = decimal.Round(revenueLastMonth, 0),
                ClassesThisWeek = weekSessions.Count,
                AtRiskMembers = await _db.Members.AsNoTracking().CountAsync(
                    m => branchIds.Contains(m.HomeBranchId)
                      && (m.ChurnRisk == ChurnRiskBand.Amber || m.ChurnRisk == ChurnRiskBand.Red)
                      && m.Status == MemberStatus.Active, ct)
            },
            Branches = branchRows,
            Revenue = revenue,
            Footfall = footfall,
            Joins = joins,
            ChurnRisk = churn.Select(m => new ChurnRiskRow
            {
                MemberId = m.Id,
                MemberCode = m.MemberCode,
                FullName = m.FullName,
                BranchName = m.BranchName,
                Band = m.ChurnRisk,
                Score = m.ChurnScore,
                LastVisitOn = m.LastVisitOn?.ToString("yyyy-MM-dd"),
                DaysSinceVisit = m.LastVisitOn is null ? 999 : today.DayNumber - m.LastVisitOn.Value.DayNumber,
                PlanName = subByMember.GetValueOrDefault(m.Id)?.PlanName,
                EndsOn = subByMember.GetValueOrDefault(m.Id)?.EndsOn.ToString("yyyy-MM-dd"),
                DuesOutstanding = duesByMember.GetValueOrDefault(m.Id),
                Phone = m.Phone
            }).ToList(),
            Expiring = expiring,
            RecentLeads = recentLeads
                .Select(l => l with { AgeDays = (int)(_clock.UtcNow - l.CreatedAtUtc).TotalDays })
                .ToList(),
            PlanMix = planMix,
            GeneratedAtIst = _clock.LocalNow.ToString("dd MMM yyyy, HH:mm")
        });
    }
}
