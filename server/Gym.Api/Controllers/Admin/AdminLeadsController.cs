using Gym.Api.Contracts;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers.Admin;

/// <summary>
/// The leads pipeline: a stage board, the follow-up queue with its automated sequences, and
/// one-step conversion into a member with a plan sold and money collected.
///
/// Moving a lead between stages is what schedules the next touch, so the queue cannot drift
/// out of sync with the board — there is no separate "remember to add a task" step.
/// </summary>
[ApiController]
[Route("api/admin/leads")]
[Authorize(Roles = RoleNames.Admin)]
[Produces("application/json")]
public class AdminLeadsController : ControllerBase
{
    private static readonly LeadStage[] BoardStages =
    {
        LeadStage.Inquiry, LeadStage.Tour, LeadStage.Trial, LeadStage.Negotiation, LeadStage.Joined, LeadStage.Lost
    };

    private readonly GymDbContext _db;
    private readonly SubscriptionService _subscriptions;
    private readonly InvoiceService _invoices;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IClock _clock;
    private readonly ILogger<AdminLeadsController> _log;

    public AdminLeadsController(
        GymDbContext db, SubscriptionService subscriptions, InvoiceService invoices,
        UserManager<ApplicationUser> users, IClock clock, ILogger<AdminLeadsController> log)
    {
        _db = db;
        _subscriptions = subscriptions;
        _invoices = invoices;
        _users = users;
        _clock = clock;
        _log = log;
    }

    [HttpGet("board")]
    [ProducesResponseType(typeof(LeadBoardResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LeadBoardResponse>> Board(
        [FromQuery] int? branchId, [FromQuery] string? assignedTo, [FromQuery] LeadSource? source,
        [FromQuery] int perColumn = 25, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var take = Math.Clamp(perColumn, 5, 100);

        var query = _db.Leads.AsNoTracking();
        if (branchId is not null) query = query.Where(l => l.BranchId == branchId);
        if (!string.IsNullOrWhiteSpace(assignedTo)) query = query.Where(l => l.AssignedTo == assignedTo);
        if (source is not null) query = query.Where(l => l.Source == source);

        var columns = new List<LeadColumn>();
        foreach (var stage in BoardStages)
        {
            var stageQuery = query.Where(l => l.Stage == stage);
            // Closed columns show only the recent past; open ones show the whole queue.
            if (stage is LeadStage.Joined or LeadStage.Lost)
            {
                var since = now.AddDays(-45);
                stageQuery = stageQuery.Where(l => l.UpdatedAtUtc >= since || l.CreatedAtUtc >= since);
            }

            var total = await stageQuery.CountAsync(ct);
            var cards = await stageQuery
                .OrderBy(l => l.NextFollowUpAtUtc ?? l.CreatedAtUtc)
                .Take(take)
                .Select(Selector(now))
                .ToListAsync(ct);

            columns.Add(new LeadColumn
            {
                Stage = stage,
                Name = StageName(stage),
                Total = total,
                Cards = cards.Select(c => c with { AgeDays = (int)(now - c.CreatedAtUtc).TotalDays }).ToList()
            });
        }

        return Ok(new LeadBoardResponse { Columns = columns, Stats = await StatsAsync(branchId, ct) });
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<LeadCard>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<LeadCard>>> List(
        [FromQuery] string? q, [FromQuery] int? branchId, [FromQuery] LeadStage? stage,
        [FromQuery] LeadSource? source, [FromQuery] bool? overdueOnly,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var query = _db.Leads.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(l => l.FullName.Contains(term) || l.Phone.Contains(term)
                                  || (l.Email != null && l.Email.Contains(term)));
        }
        if (branchId is not null) query = query.Where(l => l.BranchId == branchId);
        if (stage is not null) query = query.Where(l => l.Stage == stage);
        if (source is not null) query = query.Where(l => l.Source == source);
        if (overdueOnly == true) query = query.Where(l => l.NextFollowUpAtUtc != null && l.NextFollowUpAtUtc < now);

        var total = await query.CountAsync(ct);
        var size = Math.Clamp(pageSize, 1, 200);

        var items = await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .Skip((Math.Max(1, page) - 1) * size)
            .Take(size)
            .Select(Selector(now))
            .ToListAsync(ct);

        return Ok(new PagedResult<LeadCard>
        {
            Items = items.Select(c => c with { AgeDays = (int)(now - c.CreatedAtUtc).TotalDays }).ToList(),
            Total = total,
            Page = Math.Max(1, page),
            PageSize = size
        });
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(LeadDetailResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LeadDetailResponse>> Detail(int id, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var lead = await _db.Leads.AsNoTracking()
            .Include(l => l.Branch)
            .Include(l => l.InterestedPlan)
            .Include(l => l.FollowUps)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

        if (lead is null) return NotFound();

        return Ok(new LeadDetailResponse
        {
            Card = Describe(lead, now),
            Message = lead.Message,
            PreferredTime = lead.PreferredTime,
            UtmSource = lead.UtmSource,
            UtmCampaign = lead.UtmCampaign,
            SequenceActive = lead.SequenceActive,
            SequenceStep = lead.SequenceStep,
            FollowUps = lead.FollowUps
                .OrderBy(f => f.CompletedAtUtc is null ? 0 : 1).ThenBy(f => f.DueAtUtc)
                .Select(f => new FollowUpRow
                {
                    Id = f.Id, Channel = f.Channel, DueAtUtc = f.DueAtUtc, CompletedAtUtc = f.CompletedAtUtc,
                    Outcome = f.Outcome, Notes = f.Notes, Owner = f.Owner, IsAutomated = f.IsAutomated,
                    IsOverdue = f.CompletedAtUtc is null && f.DueAtUtc < now
                })
                .ToList()
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(LeadCard), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(UpdateLeadRequest request, CancellationToken ct)
    {
        var phone = AdminMembersController.NormalisePhone(request.Phone ?? string.Empty);
        if (string.IsNullOrWhiteSpace(request.FullName) || phone.Length < 10)
        {
            ModelState.AddModelError(nameof(request.Phone), "A lead needs a name and a 10-digit number.");
            return ValidationProblem(ModelState);
        }

        var now = _clock.UtcNow;
        var lead = new Lead
        {
            FullName = request.FullName.Trim(),
            Phone = phone,
            Email = request.Email,
            BranchId = request.BranchId,
            Source = request.Source ?? LeadSource.WalkIn,
            SourceDetail = "Added at the desk",
            Goal = request.Goal,
            Message = request.Message,
            AssignedTo = request.AssignedTo ?? User.Identity?.Name,
            InterestedPlanId = request.InterestedPlanId,
            TrialRequestedFor = request.TrialRequestedFor,
            Stage = LeadStage.Inquiry,
            SequenceActive = true,
            NextFollowUpAtUtc = now.AddHours(24),
            CreatedBy = User.Identity?.Name
        };

        lead.FollowUps.Add(new LeadFollowUp
        {
            Channel = FollowUpChannel.Call,
            DueAtUtc = now.AddHours(24),
            Notes = "First follow-up call.",
            Owner = lead.AssignedTo,
            IsAutomated = true,
            StageAtCreation = LeadStage.Inquiry
        });

        _db.Leads.Add(lead);
        await _db.SaveChangesAsync(ct);
        return Created($"/api/admin/leads/{lead.Id}", Describe(lead, now));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateLeadRequest request, CancellationToken ct)
    {
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(request.FullName)) lead.FullName = request.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            var phone = AdminMembersController.NormalisePhone(request.Phone);
            if (phone.Length < 10)
            {
                ModelState.AddModelError(nameof(request.Phone), "Enter a 10-digit mobile number.");
                return ValidationProblem(ModelState);
            }
            lead.Phone = phone;
        }
        if (request.Email is not null) lead.Email = request.Email;
        if (request.BranchId is not null) lead.BranchId = request.BranchId == 0 ? null : request.BranchId;
        if (request.Source is { } source) lead.Source = source;
        if (request.Goal is not null) lead.Goal = request.Goal;
        if (request.Message is not null) lead.Message = request.Message;
        if (request.AssignedTo is not null) lead.AssignedTo = request.AssignedTo;
        if (request.InterestedPlanId is not null)
            lead.InterestedPlanId = request.InterestedPlanId == 0 ? null : request.InterestedPlanId;
        if (request.TrialRequestedFor is not null) lead.TrialRequestedFor = request.TrialRequestedFor;
        if (request.SequenceActive is { } active) lead.SequenceActive = active;

        lead.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Moves a lead along the board. Every move records the touch, schedules the follow-up
    /// the new stage implies, and stamps the first response the first time anyone acts.
    /// </summary>
    [HttpPost("{id:int}/stage")]
    [ProducesResponseType(typeof(LeadCard), StatusCodes.Status200OK)]
    public async Task<IActionResult> Move(int id, MoveLeadRequest request, CancellationToken ct)
    {
        var lead = await _db.Leads.Include(l => l.FollowUps).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null) return NotFound();

        var now = _clock.UtcNow;
        var previous = lead.Stage;
        lead.Stage = request.Stage;
        lead.UpdatedBy = User.Identity?.Name;
        lead.FirstResponseAtUtc ??= now;

        if (request.Stage == LeadStage.Lost)
        {
            lead.LostReason = request.LostReason ?? "No reason given";
            lead.SequenceActive = false;
            lead.NextFollowUpAtUtc = null;
            foreach (var open in lead.FollowUps.Where(f => f.CompletedAtUtc is null))
            {
                open.CompletedAtUtc = now;
                open.Outcome = "Closed — lead lost";
            }
        }
        else if (request.Stage == LeadStage.Joined)
        {
            lead.SequenceActive = false;
            lead.NextFollowUpAtUtc = null;
            lead.ConvertedOn ??= _clock.Today;
        }
        else
        {
            // Each open stage has its own natural next touch; scheduling it here is what keeps
            // the follow-up queue honest without anyone remembering to add a task.
            var (channel, dueAt, note) = request.Stage switch
            {
                LeadStage.Tour => (FollowUpChannel.Call, now.AddHours(4), "Confirm the tour slot and who is showing them round."),
                LeadStage.Trial => (FollowUpChannel.WhatsApp, now.AddHours(20), "Trial reminder with directions and what to bring."),
                LeadStage.Negotiation => (FollowUpChannel.Call, now.AddHours(24), "Close the plan — quote, offer and start date."),
                _ => (FollowUpChannel.Call, now.AddHours(24), "Follow up on the enquiry.")
            };

            lead.FollowUps.Add(new LeadFollowUp
            {
                Channel = channel,
                DueAtUtc = dueAt,
                Notes = string.IsNullOrWhiteSpace(request.Note) ? note : request.Note,
                Owner = lead.AssignedTo ?? User.Identity?.Name,
                IsAutomated = true,
                StageAtCreation = request.Stage
            });
            lead.NextFollowUpAtUtc = dueAt;
            lead.SequenceStep += 1;
        }

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Lead #{LeadId} moved {From} → {To} by {User}",
            id, previous, request.Stage, User.Identity?.Name);

        return Ok(Describe(lead, now));
    }

    [HttpPost("{id:int}/followups")]
    public async Task<IActionResult> AddFollowUp(int id, CreateFollowUpRequest request, CancellationToken ct)
    {
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null) return NotFound();

        var followUp = new LeadFollowUp
        {
            LeadId = id,
            Channel = request.Channel,
            DueAtUtc = request.DueAtUtc,
            Notes = request.Notes,
            Owner = request.Owner ?? lead.AssignedTo ?? User.Identity?.Name,
            IsAutomated = false,
            StageAtCreation = lead.Stage
        };
        _db.LeadFollowUps.Add(followUp);

        if (lead.NextFollowUpAtUtc is null || request.DueAtUtc < lead.NextFollowUpAtUtc)
            lead.NextFollowUpAtUtc = request.DueAtUtc;

        await _db.SaveChangesAsync(ct);
        return Created($"/api/admin/leads/{id}", new { followUp.Id });
    }

    [HttpPost("followups/{id:int}/complete")]
    public async Task<IActionResult> CompleteFollowUp(int id, CompleteFollowUpRequest request, CancellationToken ct)
    {
        var followUp = await _db.LeadFollowUps.Include(f => f.Lead).ThenInclude(l => l.FollowUps)
            .FirstOrDefaultAsync(f => f.Id == id, ct);
        if (followUp is null) return NotFound();

        var now = _clock.UtcNow;
        followUp.CompletedAtUtc = now;
        followUp.Outcome = request.Outcome;
        if (!string.IsNullOrWhiteSpace(request.Notes))
            followUp.Notes = string.Join(" · ", new[] { followUp.Notes, request.Notes }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

        var lead = followUp.Lead;
        lead.FirstResponseAtUtc ??= now;

        if (request.NextDueAtUtc is { } nextDue)
        {
            _db.LeadFollowUps.Add(new LeadFollowUp
            {
                LeadId = lead.Id,
                Channel = request.NextChannel ?? followUp.Channel,
                DueAtUtc = nextDue,
                Owner = followUp.Owner,
                IsAutomated = false,
                StageAtCreation = lead.Stage
            });
            lead.NextFollowUpAtUtc = nextDue;
        }
        else
        {
            // The lead's due date is always the earliest task still open, or nothing.
            lead.NextFollowUpAtUtc = lead.FollowUps
                .Where(f => f.Id != id && f.CompletedAtUtc is null)
                .OrderBy(f => f.DueAtUtc)
                .Select(f => (DateTime?)f.DueAtUtc)
                .FirstOrDefault();
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Turns a lead into a member — identity, member row, and optionally the plan sale and the
    /// first payment, all in one action so the desk never re-keys a name it already has.
    /// </summary>
    [HttpPost("{id:int}/convert")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    public async Task<IActionResult> Convert(int id, ConvertLeadRequest request, CancellationToken ct)
    {
        var lead = await _db.Leads.Include(l => l.FollowUps).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null) return NotFound();
        if (lead.ConvertedMemberId is { } already)
            return Ok(new { memberId = already, alreadyConverted = true });

        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId, ct);
        if (branch is null)
        {
            ModelState.AddModelError(nameof(request.BranchId), "Pick a branch.");
            return ValidationProblem(ModelState);
        }

        // The number may already belong to a member if they enquired twice; reuse rather than
        // creating a duplicate person, which is the single worst thing a CRM can do.
        var existing = await _db.Members.FirstOrDefaultAsync(m => m.Phone == lead.Phone, ct);
        Member member;

        if (existing is not null)
        {
            member = existing;
        }
        else
        {
            var email = string.IsNullOrWhiteSpace(request.Email ?? lead.Email)
                ? $"{lead.Phone}@members.forge.local"
                : (request.Email ?? lead.Email)!.Trim().ToLowerInvariant();

            if (await _users.FindByEmailAsync(email) is not null)
            {
                ModelState.AddModelError(nameof(request.Email), "That email already has an account.");
                return ValidationProblem(ModelState);
            }

            var user = new ApplicationUser
            {
                UserName = email, Email = email, PhoneNumber = lead.Phone, FullName = lead.FullName,
                HomeBranchId = branch.Id, MustChangePassword = string.IsNullOrWhiteSpace(request.InitialPassword)
            };
            var created = await _users.CreateAsync(user,
                string.IsNullOrWhiteSpace(request.InitialPassword)
                    ? $"Forge@{Guid.NewGuid().ToString("N")[..10]}!"
                    : request.InitialPassword);

            if (!created.Succeeded)
            {
                foreach (var error in created.Errors) ModelState.AddModelError(error.Code, error.Description);
                return ValidationProblem(ModelState);
            }
            await _users.AddToRoleAsync(user, RoleNames.Member);

            var prefix = $"FRG-{branch.MemberCodePrefix}-";
            var highest = await _db.Members
                .Where(m => m.MemberCode.StartsWith(prefix))
                .Select(m => m.MemberCode).OrderByDescending(c => c).FirstOrDefaultAsync(ct);
            var next = highest is not null && int.TryParse(highest[prefix.Length..], out var parsed) ? parsed + 1 : 1;

            member = new Member
            {
                UserId = user.Id,
                MemberCode = $"{prefix}{next:D5}",
                HomeBranchId = branch.Id,
                FullName = lead.FullName,
                Phone = lead.Phone,
                Email = string.IsNullOrWhiteSpace(lead.Email) ? null : lead.Email,
                DateOfBirth = request.DateOfBirth,
                Gender = request.Gender,
                PrimaryGoal = lead.Goal,
                Status = request.PlanId is null ? MemberStatus.Trial : MemberStatus.Active,
                JoinedOn = _clock.Today,
                QrToken = Guid.NewGuid().ToString("N"),
                ReferralCode = $"FRG{1000 + user.Id}",
                CreatedBy = User.Identity?.Name
            };
            _db.Members.Add(member);
            await _db.SaveChangesAsync(ct);
        }

        object? sale = null;
        if (request.PlanId is { } planId)
        {
            try
            {
                var result = await _subscriptions.SellAsync(
                    member.Id, planId, branch.Id, request.StartsOn ?? _clock.Today,
                    request.CouponCode, null, false, 0, $"Converted from lead FRG-L{lead.Id:D5}",
                    User.Identity?.Name, ct);

                if (request.CollectMode is { } mode)
                {
                    await _invoices.RecordPaymentAsync(
                        result.Invoice, request.CollectAmount ?? result.Invoice.GrandTotal, mode,
                        User.Identity?.Name, idempotencyKey: $"lead-{lead.Id}-{result.Invoice.Id}",
                        notes: "Collected on conversion", ct: ct);
                    await _db.SaveChangesAsync(ct);
                }

                sale = new
                {
                    subscriptionId = result.Subscription.Id,
                    invoiceId = result.Invoice.Id,
                    invoiceNumber = result.Invoice.InvoiceNumber,
                    grandTotal = result.Invoice.GrandTotal,
                    amountDue = result.Invoice.AmountDue
                };
            }
            catch (InvalidOperationException ex)
            {
                return Problem($"The member was created but the plan could not be sold: {ex.Message}",
                    statusCode: StatusCodes.Status400BadRequest);
            }
        }

        var now = _clock.UtcNow;
        lead.Stage = LeadStage.Joined;
        lead.ConvertedMemberId = member.Id;
        lead.ConvertedOn = _clock.Today;
        lead.SequenceActive = false;
        lead.NextFollowUpAtUtc = null;
        lead.UpdatedBy = User.Identity?.Name;
        foreach (var open in lead.FollowUps.Where(f => f.CompletedAtUtc is null))
        {
            open.CompletedAtUtc = now;
            open.Outcome = "Joined";
        }
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Lead #{LeadId} converted to member {Code}", lead.Id, member.MemberCode);

        return Created($"/api/admin/members/{member.Id}", new
        {
            memberId = member.Id,
            memberCode = member.MemberCode,
            reusedExistingMember = existing is not null,
            sale
        });
    }

    [HttpGet("analytics")]
    [ProducesResponseType(typeof(LeadStats), StatusCodes.Status200OK)]
    public async Task<ActionResult<LeadStats>> Analytics([FromQuery] int? branchId, CancellationToken ct) =>
        Ok(await StatsAsync(branchId, ct));

    // ------------------------------------------------------------------ helpers

    private async Task<LeadStats> StatsAsync(int? branchId, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var weekAgo = now.AddDays(-7);
        var monthStart = new DateOnly(_clock.Today.Year, _clock.Today.Month, 1);

        var query = _db.Leads.AsNoTracking();
        if (branchId is not null) query = query.Where(l => l.BranchId == branchId);

        var bySource = await query
            .GroupBy(l => l.Source)
            .Select(g => new
            {
                Source = g.Key,
                Total = g.Count(),
                Joined = g.Count(l => l.Stage == LeadStage.Joined)
            })
            .ToListAsync(ct);

        var responded = await query
            .Where(l => l.FirstResponseAtUtc != null)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(200)
            .Select(l => new { l.CreatedAtUtc, l.FirstResponseAtUtc })
            .ToListAsync(ct);

        // The median, not the mean: one lead answered three days late should not move the
        // number the desk is judged on.
        var minutes = responded
            .Select(l => (l.FirstResponseAtUtc!.Value - l.CreatedAtUtc).TotalMinutes)
            .Where(m => m >= 0)
            .OrderBy(m => m)
            .ToList();
        var median = minutes.Count == 0
            ? 0m
            : (decimal)(minutes.Count % 2 == 1
                ? minutes[minutes.Count / 2]
                : (minutes[minutes.Count / 2 - 1] + minutes[minutes.Count / 2]) / 2);

        var total = await query.CountAsync(ct);
        var joined = await query.CountAsync(l => l.Stage == LeadStage.Joined, ct);

        return new LeadStats
        {
            NewThisWeek = await query.CountAsync(l => l.CreatedAtUtc >= weekAgo, ct),
            AwaitingFirstResponse = await query.CountAsync(
                l => l.FirstResponseAtUtc == null && l.Stage != LeadStage.Joined && l.Stage != LeadStage.Lost, ct),
            OverdueFollowUps = await query.CountAsync(
                l => l.NextFollowUpAtUtc != null && l.NextFollowUpAtUtc < now, ct),
            JoinedThisMonth = await query.CountAsync(l => l.ConvertedOn != null && l.ConvertedOn >= monthStart, ct),
            ConversionRate = total == 0 ? 0 : decimal.Round(joined * 100m / total, 1),
            MedianFirstResponseMinutes = decimal.Round(median, 0),
            BySource = bySource.Select(s => new SourceBreakdown
            {
                Source = s.Source,
                Name = SourceName(s.Source),
                Total = s.Total,
                Joined = s.Joined,
                ConversionRate = s.Total == 0 ? 0 : decimal.Round(s.Joined * 100m / s.Total, 1)
            }).OrderByDescending(s => s.Total).ToList()
        };
    }

    /// <summary>Shared projection so the board, the list and the dashboard show the same card.</summary>
    private static System.Linq.Expressions.Expression<Func<Lead, LeadCard>> Selector(DateTime now) => l => new LeadCard
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
        IsOverdue = l.NextFollowUpAtUtc != null && l.NextFollowUpAtUtc < now,
        AgeDays = 0,
        OpenFollowUps = l.FollowUps.Count(f => f.CompletedAtUtc == null),
        ConvertedMemberId = l.ConvertedMemberId,
        LostReason = l.LostReason
    };

    private static LeadCard Describe(Lead l, DateTime now) => new()
    {
        Id = l.Id,
        Reference = $"FRG-L{l.Id:D5}",
        FullName = l.FullName,
        Phone = l.Phone,
        Email = l.Email,
        BranchId = l.BranchId,
        BranchName = l.Branch?.Name,
        Stage = l.Stage,
        Source = l.Source,
        SourceDetail = l.SourceDetail,
        Goal = l.Goal,
        InterestedPlanName = l.InterestedPlan?.Name,
        TrialRequestedFor = l.TrialRequestedFor?.ToString("yyyy-MM-dd"),
        AssignedTo = l.AssignedTo,
        CreatedAtUtc = l.CreatedAtUtc,
        FirstResponseAtUtc = l.FirstResponseAtUtc,
        NextFollowUpAtUtc = l.NextFollowUpAtUtc,
        IsOverdue = l.NextFollowUpAtUtc is not null && l.NextFollowUpAtUtc < now,
        AgeDays = (int)(now - l.CreatedAtUtc).TotalDays,
        OpenFollowUps = l.FollowUps.Count(f => f.CompletedAtUtc is null),
        ConvertedMemberId = l.ConvertedMemberId,
        LostReason = l.LostReason
    };

    private static string StageName(LeadStage stage) => stage switch
    {
        LeadStage.Inquiry => "Inquiry",
        LeadStage.Tour => "Tour booked",
        LeadStage.Trial => "Trial",
        LeadStage.Negotiation => "Negotiation",
        LeadStage.Joined => "Joined",
        _ => "Lost"
    };

    private static string SourceName(LeadSource source) => source switch
    {
        LeadSource.WalkIn => "Walk-in",
        _ => source.ToString()
    };
}
