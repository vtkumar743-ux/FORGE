using Gym.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

public class CorporateAccountConfig : IEntityTypeConfiguration<CorporateAccount>
{
    public void Configure(EntityTypeBuilder<CorporateAccount> e)
    {
        e.Property(x => x.CompanyName).HasMaxLength(160).IsRequired();
        e.Property(x => x.Code).HasMaxLength(24).IsRequired();
        // Self-enrolment matches on the code alone, so it has to be unique network-wide.
        e.HasIndex(x => x.Code).IsUnique();
        e.Property(x => x.Domain).HasMaxLength(120);
        e.Property(x => x.HrContactName).HasMaxLength(120).IsRequired();
        e.Property(x => x.HrContactEmail).HasMaxLength(160).IsRequired();
        e.Property(x => x.HrContactPhone).HasMaxLength(20);
        e.Property(x => x.BranchScope).HasMaxLength(120);
        e.Property(x => x.Notes).AsProseNullable();
        e.HasIndex(x => new { x.IsActive, x.ValidTo });
    }
}

public class CorporateEnrolmentConfig : IEntityTypeConfiguration<CorporateEnrolment>
{
    public void Configure(EntityTypeBuilder<CorporateEnrolment> e)
    {
        e.HasOne(x => x.CorporateAccount).WithMany(x => x.Enrolments)
            .HasForeignKey(x => x.CorporateAccountId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Member).WithMany()
            .HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        e.Property(x => x.EmployeeId).HasMaxLength(40);
        e.Property(x => x.WorkEmail).HasMaxLength(160);
        // One live enrolment per member per account; ended rows stay for the usage report.
        e.HasIndex(x => new { x.CorporateAccountId, x.MemberId, x.IsActive });
        e.HasIndex(x => x.MemberId);
    }
}
