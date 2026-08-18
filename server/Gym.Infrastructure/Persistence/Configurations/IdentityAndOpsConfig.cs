using Gym.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfig : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> e)
    {
        e.ToTable("Users", "auth");
        e.Property(x => x.FullName).HasMaxLength(160).IsRequired();
        e.HasOne(x => x.HomeBranch).WithMany().HasForeignKey(x => x.HomeBranchId).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => x.HomeBranchId);
    }
}

public class ApplicationRoleConfig : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> e) => e.ToTable("Roles", "auth");
}

public class RefreshTokenConfig : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> e)
    {
        e.ToTable("RefreshTokens", "auth");
        e.Property(x => x.TokenHash).HasMaxLength(88).IsRequired();
        e.HasIndex(x => x.TokenHash).IsUnique();
        e.HasIndex(x => new { x.UserId, x.RevokedAtUtc });
        e.HasOne(x => x.User).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class BranchConfig : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> e)
    {
        e.Property(x => x.Name).HasMaxLength(120).IsRequired();
        e.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        e.HasIndex(x => x.Slug).IsUnique();
        e.Property(x => x.MemberCodePrefix).HasMaxLength(4).IsRequired();
        e.HasIndex(x => x.MemberCodePrefix).IsUnique();
        e.Property(x => x.Latitude).HasColumnType("decimal(9,6)");
        e.Property(x => x.Longitude).HasColumnType("decimal(9,6)");
        e.Property(x => x.ShortPitch).AsProseNullable();
    }
}

public class MemberConfig : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> e)
    {
        e.Property(x => x.MemberCode).HasMaxLength(32).IsRequired();
        e.HasIndex(x => x.MemberCode).IsUnique();
        e.Property(x => x.FullName).HasMaxLength(160).IsRequired();
        e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
        e.HasIndex(x => x.Phone);
        e.Property(x => x.QrToken).HasMaxLength(64).IsRequired();
        e.HasIndex(x => x.QrToken).IsUnique();
        e.Property(x => x.ReferralCode).HasMaxLength(24);
        e.HasIndex(x => x.ReferralCode);
        e.Property(x => x.MedicalNotes).AsProseNullable();
        e.Property(x => x.InjuryNotes).AsProseNullable();
        e.Property(x => x.HeightCm).HasColumnType("decimal(5,1)");
        e.Property(x => x.StartWeightKg).HasColumnType("decimal(5,1)");

        e.HasOne(x => x.User).WithOne(x => x.Member).HasForeignKey<Member>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.HomeBranch).WithMany(x => x.Members).HasForeignKey(x => x.HomeBranchId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.ReferredByMember).WithMany().HasForeignKey(x => x.ReferredByMemberId).OnDelete(DeleteBehavior.NoAction);

        // Hot admin filters: roster by branch+status, churn radar, absentee sweep.
        e.HasIndex(x => new { x.HomeBranchId, x.Status });
        e.HasIndex(x => new { x.ChurnRisk, x.LastVisitOn });
    }
}

public class TrainerConfig : IEntityTypeConfiguration<Trainer>
{
    public void Configure(EntityTypeBuilder<Trainer> e)
    {
        e.Property(x => x.FullName).HasMaxLength(160).IsRequired();
        e.Property(x => x.Slug).HasMaxLength(140).IsRequired();
        e.HasIndex(x => x.Slug).IsUnique();
        e.Property(x => x.Bio).AsProse();
        e.Property(x => x.Headline).HasMaxLength(240);
        e.Property(x => x.AverageRating).HasColumnType("decimal(3,2)");
        e.Property(x => x.CommissionPercent).HasColumnType("decimal(5,2)");

        e.HasOne(x => x.User).WithOne(x => x.Trainer).HasForeignKey<Trainer>(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        e.HasOne(x => x.PrimaryBranch).WithMany().HasForeignKey(x => x.PrimaryBranchId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.PrimaryBranchId, x.IsActive });
    }
}

public class TrainerRatingConfig : IEntityTypeConfiguration<TrainerRating>
{
    public void Configure(EntityTypeBuilder<TrainerRating> e)
    {
        e.Property(x => x.Comment).AsProseNullable();
        e.HasOne(x => x.Trainer).WithMany(x => x.Ratings).HasForeignKey(x => x.TrainerId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.ClassSession).WithMany().HasForeignKey(x => x.ClassSessionId).OnDelete(DeleteBehavior.NoAction);
        e.HasIndex(x => new { x.TrainerId, x.CreatedAtUtc });
    }
}
