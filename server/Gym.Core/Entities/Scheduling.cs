using Gym.Core.Enums;

namespace Gym.Core.Entities;

/// <summary>Class format library — shared across every branch (multi-branch essential).</summary>
public class ClassFormat : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DefaultDurationMinutes { get; set; } = 45;
    public int DefaultCapacity { get; set; } = 20;
    public ClassLevel Level { get; set; }
    public ClassIntensity Intensity { get; set; }
    public int EstimatedCalories { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? IconKey { get; set; }
    /// <summary>Comma-separated tags used by the public timetable filter pills.</summary>
    public string? Tags { get; set; }
    public bool ShowOnWebsite { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    public ICollection<ClassSchedule> Schedules { get; set; } = new List<ClassSchedule>();
}

public class Room : BaseEntity
{
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Recurring rule for a weekly slot. Concrete occurrences are materialised into
/// <see cref="ClassSession"/> rows so bookings, rosters and no-shows attach to a real date.
/// </summary>
public class ClassSchedule : BaseEntity
{
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public int ClassFormatId { get; set; }
    public ClassFormat ClassFormat { get; set; } = null!;
    public int? RoomId { get; set; }
    public Room? Room { get; set; }
    public int TrainerId { get; set; }
    public Trainer Trainer { get; set; } = null!;

    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public int DurationMinutes { get; set; }
    public int Capacity { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>Booking opens this many hours before start.</summary>
    public int BookingOpensHoursBefore { get; set; } = 72;
    /// <summary>Free-cancel cut-off; inside it a cancel counts as a late cancel.</summary>
    public int CancelCutoffHoursBefore { get; set; } = 4;
    public bool WaitlistEnabled { get; set; } = true;
    public int WaitlistCapacity { get; set; } = 10;
    public bool IsActive { get; set; } = true;

    public ICollection<ClassSession> Sessions { get; set; } = new List<ClassSession>();
}

public class ClassSession : BaseEntity
{
    public int ClassScheduleId { get; set; }
    public ClassSchedule ClassSchedule { get; set; } = null!;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public int ClassFormatId { get; set; }
    public ClassFormat ClassFormat { get; set; } = null!;
    public int TrainerId { get; set; }
    public Trainer Trainer { get; set; } = null!;
    /// <summary>Set when the rostered trainer is swapped out for this date only.</summary>
    public int? SubstituteTrainerId { get; set; }
    public Trainer? SubstituteTrainer { get; set; }
    public int? RoomId { get; set; }
    public Room? Room { get; set; }

    public DateOnly SessionDate { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public TimeOnly StartTime { get; set; }
    public int DurationMinutes { get; set; }

    public int Capacity { get; set; }
    /// <summary>Denormalised counters — the capacity ring reads these, not a COUNT(*).</summary>
    public int BookedCount { get; set; }
    public int WaitlistCount { get; set; }
    public int AttendedCount { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;
    public string? CancellationReason { get; set; }

    public int SpotsLeft => Math.Max(0, Capacity - BookedCount);

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

public class Booking : BaseEntity
{
    public int ClassSessionId { get; set; }
    public ClassSession ClassSession { get; set; } = null!;
    public int MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public int? SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Booked;
    public DateTime BookedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAtUtc { get; set; }
    public bool WasLateCancel { get; set; }
    public DateTime? CheckedInAtUtc { get; set; }

    /// <summary>1-based queue position; promoted to a booking when a spot frees up.</summary>
    public int? WaitlistPosition { get; set; }
    public DateTime? PromotedFromWaitlistAtUtc { get; set; }
    public bool CreditConsumed { get; set; }
    public int? RatingScore { get; set; }
    public string? RatingComment { get; set; }
    public string? Notes { get; set; }
}
