using Gym.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

public class ExerciseConfig : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> e)
    {
        e.Property(x => x.Name).HasMaxLength(140).IsRequired();
        e.Property(x => x.Slug).HasMaxLength(140).IsRequired();
        e.HasIndex(x => x.Slug).IsUnique();
        e.Property(x => x.Cues).AsProseNullable();
        e.HasIndex(x => new { x.PrimaryMuscle, x.Equipment });
    }
}

public class WorkoutProgramConfig : IEntityTypeConfiguration<WorkoutProgram>
{
    public void Configure(EntityTypeBuilder<WorkoutProgram> e)
    {
        e.Property(x => x.Name).HasMaxLength(160).IsRequired();
        e.Property(x => x.Description).AsProseNullable();
        e.Property(x => x.GenerationContextJson).AsJsonNullable();
        e.HasOne(x => x.Member).WithMany(x => x.Programs).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Trainer).WithMany().HasForeignKey(x => x.TrainerId).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => new { x.MemberId, x.Status });
    }
}

public class ProgramDayConfig : IEntityTypeConfiguration<ProgramDay>
{
    public void Configure(EntityTypeBuilder<ProgramDay> e)
    {
        e.Property(x => x.Title).HasMaxLength(160).IsRequired();
        e.Property(x => x.Notes).AsProseNullable();
        e.HasOne(x => x.WorkoutProgram).WithMany(x => x.Days).HasForeignKey(x => x.WorkoutProgramId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.WorkoutProgramId, x.DayIndex }).IsUnique();
    }
}

public class ProgramExerciseConfig : IEntityTypeConfiguration<ProgramExercise>
{
    public void Configure(EntityTypeBuilder<ProgramExercise> e)
    {
        e.Property(x => x.TargetWeightKg).HasColumnType("decimal(6,2)");
        e.HasOne(x => x.ProgramDay).WithMany(x => x.Exercises).HasForeignKey(x => x.ProgramDayId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Exercise).WithMany().HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.ProgramDayId, x.OrderIndex });
    }
}

public class WorkoutLogConfig : IEntityTypeConfiguration<WorkoutLog>
{
    public void Configure(EntityTypeBuilder<WorkoutLog> e)
    {
        e.Property(x => x.WeightKg).HasColumnType("decimal(6,2)");
        e.Property(x => x.Volume).HasColumnType("decimal(10,2)");
        e.Property(x => x.EstimatedOneRepMax).HasColumnType("decimal(6,2)");
        e.Property(x => x.DistanceKm).HasColumnType("decimal(6,2)");
        e.Property(x => x.Notes).AsProseNullable();

        e.HasOne(x => x.Member).WithMany(x => x.WorkoutLogs).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Exercise).WithMany().HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.ProgramExercise).WithMany().HasForeignKey(x => x.ProgramExerciseId).OnDelete(DeleteBehavior.NoAction);

        // PR detection compares a new set against the member's best for that lift.
        e.HasIndex(x => new { x.MemberId, x.ExerciseId, x.EstimatedOneRepMax });
        e.HasIndex(x => new { x.MemberId, x.PerformedOn });
    }
}

public class DietPlanConfig : IEntityTypeConfiguration<DietPlan>
{
    public void Configure(EntityTypeBuilder<DietPlan> e)
    {
        e.Property(x => x.Name).HasMaxLength(160).IsRequired();
        e.Property(x => x.Notes).AsProseNullable();
        e.Property(x => x.GenerationContextJson).AsJsonNullable();
        e.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Trainer).WithMany().HasForeignKey(x => x.TrainerId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class MealConfig : IEntityTypeConfiguration<Meal>
{
    public void Configure(EntityTypeBuilder<Meal> e)
    {
        e.Property(x => x.Title).HasMaxLength(160).IsRequired();
        e.Property(x => x.Items).AsProse();
        e.HasOne(x => x.DietPlan).WithMany(x => x.Meals).HasForeignKey(x => x.DietPlanId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.DietPlanId, x.OrderIndex });
    }
}

public class BodyScanConfig : IEntityTypeConfiguration<BodyScan>
{
    public void Configure(EntityTypeBuilder<BodyScan> e)
    {
        foreach (var name in new[]
                 {
                     nameof(BodyScan.WeightKg), nameof(BodyScan.BodyFatPercent), nameof(BodyScan.SkeletalMuscleMassKg),
                     nameof(BodyScan.FatMassKg), nameof(BodyScan.VisceralFatLevel), nameof(BodyScan.Bmi),
                     nameof(BodyScan.BasalMetabolicRate), nameof(BodyScan.TotalBodyWaterL), nameof(BodyScan.InBodyScore),
                     nameof(BodyScan.ChestCm), nameof(BodyScan.WaistCm), nameof(BodyScan.HipCm),
                     nameof(BodyScan.ThighCm), nameof(BodyScan.ArmCm)
                 })
            e.Property(name).HasColumnType("decimal(6,2)");

        e.Property(x => x.Notes).AsProseNullable();
        e.HasOne(x => x.Member).WithMany(x => x.BodyScans).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.MemberId, x.ScanDate });
    }
}

public class ProgressPhotoConfig : IEntityTypeConfiguration<ProgressPhoto>
{
    public void Configure(EntityTypeBuilder<ProgressPhoto> e)
    {
        e.Property(x => x.WeightKg).HasColumnType("decimal(5,1)");
        e.Property(x => x.Pose).HasMaxLength(24);
        e.HasOne(x => x.Member).WithMany(x => x.ProgressPhotos).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.MemberId, x.TakenOn });
    }
}

public class ProductConfig : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> e)
    {
        e.Property(x => x.Name).HasMaxLength(160).IsRequired();
        e.Property(x => x.Sku).HasMaxLength(48).IsRequired();
        e.HasIndex(x => x.Sku).IsUnique();
        e.Property(x => x.Barcode).HasMaxLength(48);
        e.Property(x => x.HsnCode).HasMaxLength(12);
        e.Property(x => x.GstRatePercent).HasColumnType("decimal(5,2)");
        e.Property(x => x.Description).AsProseNullable();
    }
}

public class BranchStockConfig : IEntityTypeConfiguration<BranchStock>
{
    public void Configure(EntityTypeBuilder<BranchStock> e)
    {
        e.HasOne(x => x.Branch).WithMany(x => x.Stock).HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Product).WithMany(x => x.Stock).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.BranchId, x.ProductId }).IsUnique();
    }
}

public class StockTransferConfig : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> e)
    {
        e.Property(x => x.Notes).AsProseNullable();
        e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.FromBranch).WithMany().HasForeignKey(x => x.FromBranchId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.ToBranch).WithMany().HasForeignKey(x => x.ToBranchId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.Status, x.RequestedAtUtc });
    }
}

public class OrderConfig : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> e)
    {
        e.Property(x => x.OrderNumber).HasMaxLength(40).IsRequired();
        e.HasIndex(x => x.OrderNumber).IsUnique();
        e.Property(x => x.Notes).AsProseNullable();
        e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => new { x.BranchId, x.PlacedAtUtc });
    }
}

public class OrderLineConfig : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> e)
    {
        e.Property(x => x.GstRatePercent).HasColumnType("decimal(5,2)");
        e.HasOne(x => x.Order).WithMany(x => x.Lines).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
