using Gym.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

public class ClassFormatConfig : IEntityTypeConfiguration<ClassFormat>
{
    public void Configure(EntityTypeBuilder<ClassFormat> e)
    {
        e.Property(x => x.Name).HasMaxLength(120).IsRequired();
        e.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        e.HasIndex(x => x.Slug).IsUnique();
        e.Property(x => x.Description).AsProse();
        e.Property(x => x.ShortDescription).HasMaxLength(300);
    }
}

public class RoomConfig : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> e)
    {
        e.Property(x => x.Name).HasMaxLength(120).IsRequired();
        e.HasOne(x => x.Branch).WithMany(x => x.Rooms).HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.BranchId, x.Name }).IsUnique();
    }
}

public class ClassScheduleConfig : IEntityTypeConfiguration<ClassSchedule>
{
    public void Configure(EntityTypeBuilder<ClassSchedule> e)
    {
        e.HasOne(x => x.Branch).WithMany(x => x.Schedules).HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.ClassFormat).WithMany(x => x.Schedules).HasForeignKey(x => x.ClassFormatId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Trainer).WithMany(x => x.Schedules).HasForeignKey(x => x.TrainerId).OnDelete(DeleteBehavior.Restrict);
        // NoAction, not SetNull: Branch cascades to both Rooms and Schedules, and a second
        // action-carrying path into Schedules would trip SQL Server's multiple-cascade-path check.
        e.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.NoAction);

        // Conflict detection reads by trainer/day/time and by room/day/time.
        e.HasIndex(x => new { x.BranchId, x.DayOfWeek, x.StartTime });
        e.HasIndex(x => new { x.TrainerId, x.DayOfWeek, x.StartTime });
    }
}

public class ClassSessionConfig : IEntityTypeConfiguration<ClassSession>
{
    public void Configure(EntityTypeBuilder<ClassSession> e)
    {
        e.Ignore(x => x.SpotsLeft);
        e.HasOne(x => x.ClassSchedule).WithMany(x => x.Sessions).HasForeignKey(x => x.ClassScheduleId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.ClassFormat).WithMany().HasForeignKey(x => x.ClassFormatId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Trainer).WithMany().HasForeignKey(x => x.TrainerId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.SubstituteTrainer).WithMany().HasForeignKey(x => x.SubstituteTrainerId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.NoAction);

        // One occurrence per schedule per date — protects the materialiser from double-runs.
        e.HasIndex(x => new { x.ClassScheduleId, x.SessionDate }).IsUnique();
        // The public timetable query.
        e.HasIndex(x => new { x.BranchId, x.SessionDate, x.StartTime });
    }
}

public class BookingConfig : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> e)
    {
        e.HasOne(x => x.ClassSession).WithMany(x => x.Bookings).HasForeignKey(x => x.ClassSessionId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Member).WithMany(x => x.Bookings).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Subscription).WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.NoAction);
        e.Property(x => x.RatingComment).AsProseNullable();

        // A member holds at most one live row per session (booked or waitlisted).
        e.HasIndex(x => new { x.ClassSessionId, x.MemberId, x.Status });
        e.HasIndex(x => new { x.MemberId, x.BookedAtUtc });
        e.HasIndex(x => new { x.ClassSessionId, x.WaitlistPosition });
    }
}

public class CheckInConfig : IEntityTypeConfiguration<CheckIn>
{
    public void Configure(EntityTypeBuilder<CheckIn> e)
    {
        e.Ignore(x => x.DurationMinutes);
        e.HasOne(x => x.Member).WithMany(x => x.CheckIns).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Branch).WithMany(x => x.CheckIns).HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.ClassSession).WithMany().HasForeignKey(x => x.ClassSessionId).OnDelete(DeleteBehavior.NoAction);

        // Occupancy meter (open visits now) and the peak-hours heatmap.
        e.HasIndex(x => new { x.BranchId, x.VisitDate });
        e.HasIndex(x => new { x.BranchId, x.CheckOutAtUtc });
        e.HasIndex(x => new { x.MemberId, x.VisitDate });
    }
}

public class LeadConfig : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> e)
    {
        e.Property(x => x.FullName).HasMaxLength(160).IsRequired();
        e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
        e.Property(x => x.Message).AsProseNullable();
        e.HasIndex(x => x.Phone);

        e.HasOne(x => x.Branch).WithMany(x => x.Leads).HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.SetNull);
        e.HasOne(x => x.InterestedPlan).WithMany().HasForeignKey(x => x.InterestedPlanId).OnDelete(DeleteBehavior.SetNull);
        e.HasOne(x => x.ConvertedMember).WithMany().HasForeignKey(x => x.ConvertedMemberId).OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(x => new { x.Stage, x.CreatedAtUtc });
        e.HasIndex(x => new { x.BranchId, x.Stage });
        e.HasIndex(x => x.NextFollowUpAtUtc);
    }
}

public class LeadFollowUpConfig : IEntityTypeConfiguration<LeadFollowUp>
{
    public void Configure(EntityTypeBuilder<LeadFollowUp> e)
    {
        e.Property(x => x.Notes).AsProseNullable();
        e.HasOne(x => x.Lead).WithMany(x => x.FollowUps).HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.DueAtUtc, x.CompletedAtUtc });
    }
}
