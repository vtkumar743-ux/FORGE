using Gym.Core.Enums;

namespace Gym.Core.Entities;

public class Referral : BaseEntity
{
    public int ReferrerMemberId { get; set; }
    public Member ReferrerMember { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string? InviteeName { get; set; }
    public string? InviteePhone { get; set; }
    public int? InviteeMemberId { get; set; }
    public Member? InviteeMember { get; set; }
    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }

    public ReferralStatus Status { get; set; } = ReferralStatus.Sent;
    /// <summary>₹500 credit both sides, per the spec.</summary>
    public decimal RewardAmount { get; set; } = 500m;
    public bool ReferrerRewarded { get; set; }
    public bool InviteeRewarded { get; set; }
    public DateTime? ConvertedAtUtc { get; set; }
    public DateOnly? ExpiresOn { get; set; }
}

public class Badge : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>Key into the inline-SVG icon registry — never an emoji.</summary>
    public string IconKey { get; set; } = "medal";
    public string Tier { get; set; } = "bronze";
    /// <summary>Machine-readable award rule, e.g. {"metric":"checkIns","threshold":50}.</summary>
    public string CriteriaJson { get; set; } = "{}";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class MemberBadge : BaseEntity
{
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public int BadgeId { get; set; }
    public Badge Badge { get; set; } = null!;
    public DateTime AwardedAtUtc { get; set; } = DateTime.UtcNow;
    public string? Context { get; set; }
    public bool IsSeen { get; set; }
}

public class Challenge : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ChallengeMetric Metric { get; set; }
    public decimal TargetValue { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? RewardDescription { get; set; }
    public bool IsActive { get; set; } = true;
}

public class FeedPost : BaseEntity
{
    public FeedPostKind Kind { get; set; }
    public int? MemberId { get; set; }
    public Member? Member { get; set; }
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? ImageUrl { get; set; }
    /// <summary>Structured payload for PR shout-outs: lift, weight, previous best.</summary>
    public string? MetaJson { get; set; }
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public bool IsPinned { get; set; }
    public bool IsVisible { get; set; } = true;
    public DateTime PostedAtUtc { get; set; } = DateTime.UtcNow;
}

public class Notification : BaseEntity
{
    public int? MemberId { get; set; }
    public Member? Member { get; set; }
    public int? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public NotificationKind Kind { get; set; }
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public string? TemplateKey { get; set; }
    public string? PayloadJson { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public bool DeliveryFailed { get; set; }
    public string? DeliveryError { get; set; }
}
