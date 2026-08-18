using Gym.Core.Enums;

namespace Gym.Core.Entities;

/// <summary>
/// One row per visit. Live occupancy is derived from rows where CheckOut is null,
/// and the peak-hours heatmap from CheckInAtUtc buckets.
/// </summary>
public class CheckIn : BaseEntity
{
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public int? ClassSessionId { get; set; }
    public ClassSession? ClassSession { get; set; }

    public DateTime CheckInAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CheckOutAtUtc { get; set; }
    public DateOnly VisitDate { get; set; }
    public CheckInSource Source { get; set; } = CheckInSource.Qr;
    public string? DeviceId { get; set; }
    /// <summary>Set when the turnstile/kiosk refused entry (expired plan, dues outstanding).</summary>
    public bool WasBlocked { get; set; }
    public string? BlockReason { get; set; }
    public string? RecordedBy { get; set; }

    public int? DurationMinutes => CheckOutAtUtc is null
        ? null
        : (int)(CheckOutAtUtc.Value - CheckInAtUtc).TotalMinutes;
}

public class Lead : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public LeadStage Stage { get; set; } = LeadStage.Inquiry;
    public LeadSource Source { get; set; } = LeadSource.Website;
    public string? SourceDetail { get; set; }
    public string? Goal { get; set; }
    public string? Message { get; set; }
    public string? PreferredTime { get; set; }
    public DateOnly? TrialRequestedFor { get; set; }
    public int? InterestedPlanId { get; set; }
    public Plan? InterestedPlan { get; set; }

    public string? AssignedTo { get; set; }
    public DateTime? FirstResponseAtUtc { get; set; }
    public DateTime? NextFollowUpAtUtc { get; set; }
    public DateOnly? ConvertedOn { get; set; }
    public int? ConvertedMemberId { get; set; }
    public Member? ConvertedMember { get; set; }
    public string? LostReason { get; set; }
    /// <summary>Set when the automated sequence (5-min response, trial reminder…) is running.</summary>
    public bool SequenceActive { get; set; } = true;
    public int SequenceStep { get; set; }
    public string? UtmSource { get; set; }
    public string? UtmCampaign { get; set; }

    public ICollection<LeadFollowUp> FollowUps { get; set; } = new List<LeadFollowUp>();
}

public class LeadFollowUp : BaseEntity
{
    public int LeadId { get; set; }
    public Lead Lead { get; set; } = null!;

    public FollowUpChannel Channel { get; set; }
    public DateTime DueAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? Outcome { get; set; }
    public string? Notes { get; set; }
    public string? Owner { get; set; }
    public bool IsAutomated { get; set; }
    public LeadStage StageAtCreation { get; set; }
}
