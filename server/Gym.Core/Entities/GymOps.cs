using Gym.Core.Enums;

namespace Gym.Core.Entities;

public class Branch : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    /// <summary>
    /// Three-letter code embedded in member numbers (FRG-KOR-00184). Stored rather than
    /// derived from the slug so the seeder, the registration endpoint and any branch the
    /// owner adds later all mint numbers in the same series.
    /// </summary>
    public string MemberCodePrefix { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string State { get; set; } = string.Empty;
    public string Pincode { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? WhatsAppNumber { get; set; }
    public string Email { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string? GoogleMapsUrl { get; set; }
    public string? GooglePlaceId { get; set; }

    public TimeOnly WeekdayOpen { get; set; } = new(5, 30);
    public TimeOnly WeekdayClose { get; set; } = new(23, 0);
    public TimeOnly WeekendOpen { get; set; } = new(6, 0);
    public TimeOnly WeekendClose { get; set; } = new(22, 0);

    public int FloorAreaSqft { get; set; }
    /// <summary>Head-count the floor is comfortable with — denominator of the occupancy meter.</summary>
    public int OccupancyCapacity { get; set; } = 120;
    public string? Gstin { get; set; }
    public string? HeroImageUrl { get; set; }
    public string? TourVideoUrl { get; set; }
    public string? ShortPitch { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    public ICollection<Room> Rooms { get; set; } = new List<Room>();
    public ICollection<Member> Members { get; set; } = new List<Member>();
    public ICollection<PlanBranchPrice> PlanPrices { get; set; } = new List<PlanBranchPrice>();
    public ICollection<ClassSchedule> Schedules { get; set; } = new List<ClassSchedule>();
    public ICollection<CheckIn> CheckIns { get; set; } = new List<CheckIn>();
    public ICollection<Lead> Leads { get; set; } = new List<Lead>();
    public ICollection<BranchStock> Stock { get; set; } = new List<BranchStock>();
}

public class Member : BaseEntity
{
    public int UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    /// <summary>Human-facing code printed on the card, e.g. FRG-KOR-00184.</summary>
    public string MemberCode { get; set; } = string.Empty;
    public int HomeBranchId { get; set; }
    public Branch HomeBranch { get; set; } = null!;

    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string? PhotoUrl { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? Pincode { get; set; }

    public MemberStatus Status { get; set; } = MemberStatus.Active;
    public DateOnly JoinedOn { get; set; }
    public string? PrimaryGoal { get; set; }
    public string? MedicalNotes { get; set; }
    public string? InjuryNotes { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? StartWeightKg { get; set; }

    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public bool WaiverSigned { get; set; }
    public DateTime? WaiverSignedAtUtc { get; set; }
    public string? WaiverDocumentUrl { get; set; }

    /// <summary>Stable payload encoded into the member's QR for desk check-in.</summary>
    public string QrToken { get; set; } = string.Empty;
    public string? ReferralCode { get; set; }
    public int? ReferredByMemberId { get; set; }
    public Member? ReferredByMember { get; set; }
    public string? CorporateCode { get; set; }

    /// <summary>Comma-separated admin tags (vip, pt-client, student…).</summary>
    public string? Tags { get; set; }
    public bool ConsentMarketing { get; set; } = true;
    public bool ConsentLeaderboard { get; set; }
    public bool ConsentTransformationShowcase { get; set; }

    public int CurrentStreakDays { get; set; }
    public int LongestStreakDays { get; set; }
    public DateOnly? LastVisitOn { get; set; }
    public ChurnRiskBand ChurnRisk { get; set; } = ChurnRiskBand.Healthy;
    public int ChurnScore { get; set; }
    /// <summary>Why the radar flagged them — shown on the row so the desk knows what to open with.</summary>
    public string? ChurnReasons { get; set; }
    public DateTime? ChurnScoredAtUtc { get; set; }
    /// <summary>Stops the same person receiving a second win-back before the first has had time to work.</summary>
    public DateTime? LastWinBackAtUtc { get; set; }

    public int? CorporateAccountId { get; set; }
    public CorporateAccount? CorporateAccount { get; set; }

    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<CheckIn> CheckIns { get; set; } = new List<CheckIn>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<BodyScan> BodyScans { get; set; } = new List<BodyScan>();
    public ICollection<ProgressPhoto> ProgressPhotos { get; set; } = new List<ProgressPhoto>();
    public ICollection<WorkoutProgram> Programs { get; set; } = new List<WorkoutProgram>();
    public ICollection<WorkoutLog> WorkoutLogs { get; set; } = new List<WorkoutLog>();
    public ICollection<MemberBadge> Badges { get; set; } = new List<MemberBadge>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

public class Trainer : BaseEntity
{
    public int? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public Gender Gender { get; set; }

    /// <summary>3:4 portrait per the design system's trainer-card rule.</summary>
    public string? PortraitUrl { get; set; }
    public string? DemoVideoUrl { get; set; }
    public string Headline { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    /// <summary>Comma-separated: "Strength, Olympic lifting, Mobility".</summary>
    public string Specialties { get; set; } = string.Empty;
    /// <summary>Comma-separated: "NSCA-CSCS, ACE-CPT, K11".</summary>
    public string Certifications { get; set; } = string.Empty;
    public int YearsExperience { get; set; }
    public string? InstagramUrl { get; set; }

    public int PrimaryBranchId { get; set; }
    public Branch PrimaryBranch { get; set; } = null!;

    public decimal PerClassRate { get; set; }
    public decimal PerPtSessionRate { get; set; }
    public decimal CommissionPercent { get; set; }
    public decimal PtSessionPrice { get; set; }

    public decimal AverageRating { get; set; }
    public int RatingCount { get; set; }
    public bool AcceptsPtClients { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public bool ShowOnWebsite { get; set; } = true;
    public int DisplayOrder { get; set; }

    public ICollection<ClassSchedule> Schedules { get; set; } = new List<ClassSchedule>();
    public ICollection<TrainerRating> Ratings { get; set; } = new List<TrainerRating>();
}

public class TrainerRating : BaseEntity
{
    public int TrainerId { get; set; }
    public Trainer Trainer { get; set; } = null!;
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public int? ClassSessionId { get; set; }
    public ClassSession? ClassSession { get; set; }

    /// <summary>1–5.</summary>
    public int Score { get; set; }
    public string? Comment { get; set; }
    public bool IsPublished { get; set; }
}
