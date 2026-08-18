using Gym.Api.Contracts;
using Gym.Core.Entities;
using Gym.Core.Enums;
using Gym.Core.Interfaces;
using Gym.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api.Controllers;

/// <summary>
/// Free-trial, tour and PT enquiries from the public site (Module 1.6). Every submission
/// lands in the same Leads pipeline the admin board reads in Phase 2, with its automated
/// follow-up sequence already scheduled — so nothing captured here waits on a later phase
/// to become actionable.
/// </summary>
[ApiController]
[Route("api/leads")]
[Produces("application/json")]
public class LeadsController : ControllerBase
{
    private readonly GymDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<LeadsController> _log;

    public LeadsController(GymDbContext db, IClock clock, ILogger<LeadsController> log)
    {
        _db = db;
        _clock = clock;
        _log = log;
    }

    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CreateLeadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateLeadResponse>> Create(
        [FromBody] CreateLeadRequest request, CancellationToken ct)
    {
        // Honeypot: the field is visually hidden, so anything in it is a bot. Answer 201 anyway —
        // telling a scraper it was detected only teaches it to try again differently.
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            _log.LogInformation("Lead submission rejected by honeypot from {Ip}", HttpContext.Connection.RemoteIpAddress);
            return StatusCode(StatusCodes.Status201Created, new CreateLeadResponse
            {
                Id = 0,
                Reference = "PENDING",
                FirstFollowUpAtUtc = _clock.UtcNow
            });
        }

        var phone = NormalisePhone(request.Phone);
        if (phone.Length < 10)
        {
            ModelState.AddModelError(nameof(request.Phone), "Enter a 10-digit mobile number.");
            return ValidationProblem(ModelState);
        }

        var branch = string.IsNullOrWhiteSpace(request.BranchSlug)
            ? null
            : await _db.Branches.AsNoTracking()
                .Where(b => b.Slug == request.BranchSlug && b.IsActive)
                .Select(b => new { b.Id, b.Name, b.WhatsAppNumber })
                .FirstOrDefaultAsync(ct);

        int? planId = string.IsNullOrWhiteSpace(request.PlanSlug)
            ? null
            : await _db.Plans.AsNoTracking()
                .Where(p => p.Slug == request.PlanSlug)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync(ct);

        var now = _clock.UtcNow;

        // A repeat submission within the day is the same person double-tapping, not a new lead.
        var since = now.AddHours(-24);
        var existing = await _db.Leads
            .Where(l => l.Phone == phone && l.CreatedAtUtc >= since)
            .OrderByDescending(l => l.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            _log.LogInformation("Duplicate lead for {Phone} folded into #{LeadId}", Mask(phone), existing.Id);
            return Ok(new CreateLeadResponse
            {
                Id = existing.Id,
                Reference = Reference(existing.Id),
                BranchName = branch?.Name,
                WhatsAppNumber = branch?.WhatsAppNumber,
                FirstFollowUpAtUtc = existing.NextFollowUpAtUtc ?? now
            });
        }

        var intent = (request.Intent ?? "trial").Trim().ToLowerInvariant();

        var lead = new Lead
        {
            FullName = request.FullName.Trim(),
            Phone = phone,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            BranchId = branch?.Id,
            Stage = intent == "tour" ? LeadStage.Tour : LeadStage.Inquiry,
            Source = LeadSource.Website,
            SourceDetail = intent switch
            {
                "tour" => "Website — tour request",
                "pt" => "Website — personal training enquiry",
                "join" => "Website — online joining",
                "callback" => "Website — callback request",
                _ => "Website — free trial form"
            },
            Goal = request.Goal,
            Message = request.Message,
            PreferredTime = request.PreferredTime,
            TrialRequestedFor = request.TrialDate,
            InterestedPlanId = planId,
            // The 5-minute response window is the whole point of the sequence: leads contacted
            // inside it convert several times better than ones chased the next morning.
            NextFollowUpAtUtc = now.AddMinutes(5),
            SequenceActive = true,
            SequenceStep = 0,
            UtmSource = request.UtmSource,
            UtmCampaign = request.UtmCampaign
        };

        lead.FollowUps.Add(Step(lead, FollowUpChannel.WhatsApp, now.AddMinutes(5),
            "Confirm the booking, name the coach on duty and say what to bring."));
        lead.FollowUps.Add(Step(lead, FollowUpChannel.Call, now.AddHours(24),
            "Call if the WhatsApp confirmation went unanswered."));

        if (request.TrialDate is { } trialDate)
        {
            // Reminder the evening before, and the nudge that actually converts: same-day follow-up.
            var eveningBefore = trialDate.AddDays(-1).ToDateTime(new TimeOnly(19, 0)).AddMinutes(-330);
            if (eveningBefore > now)
                lead.FollowUps.Add(Step(lead, FollowUpChannel.WhatsApp, eveningBefore,
                    "Reminder for tomorrow's trial with directions and parking."));

            var afterTrial = trialDate.ToDateTime(new TimeOnly(20, 0)).AddMinutes(-330);
            lead.FollowUps.Add(Step(lead, FollowUpChannel.Call, afterTrial,
                "Post-trial call: how was the session, which plan fits."));
        }

        _db.Leads.Add(lead);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Website lead #{LeadId} captured for {Branch} ({Intent})",
            lead.Id, branch?.Name ?? "no branch", intent);

        var response = new CreateLeadResponse
        {
            Id = lead.Id,
            Reference = Reference(lead.Id),
            BranchName = branch?.Name,
            WhatsAppNumber = branch?.WhatsAppNumber,
            FirstFollowUpAtUtc = lead.NextFollowUpAtUtc!.Value
        };

        return StatusCode(StatusCodes.Status201Created, response);
    }

    private static LeadFollowUp Step(Lead lead, FollowUpChannel channel, DateTime dueUtc, string notes) => new()
    {
        Lead = lead,
        Channel = channel,
        DueAtUtc = dueUtc,
        Notes = notes,
        IsAutomated = true,
        StageAtCreation = lead.Stage
    };

    /// <summary>Strips +91, spaces and dashes down to the ten digits the desk dials.</summary>
    private static string NormalisePhone(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length > 10 && digits.StartsWith("91")) digits = digits[^10..];
        if (digits.Length > 10 && digits.StartsWith('0')) digits = digits[^10..];
        return digits;
    }

    private static string Reference(int id) => $"FRG-L{id:D5}";

    private static string Mask(string phone) => phone.Length < 4 ? "****" : $"******{phone[^4..]}";
}
