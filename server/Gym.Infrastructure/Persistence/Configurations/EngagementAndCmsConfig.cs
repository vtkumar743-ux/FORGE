using Gym.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

public class ReferralConfig : IEntityTypeConfiguration<Referral>
{
    public void Configure(EntityTypeBuilder<Referral> e)
    {
        e.Property(x => x.Code).HasMaxLength(24).IsRequired();
        e.HasIndex(x => x.Code);
        e.HasOne(x => x.ReferrerMember).WithMany().HasForeignKey(x => x.ReferrerMemberId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.InviteeMember).WithMany().HasForeignKey(x => x.InviteeMemberId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne(x => x.Lead).WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class BadgeConfig : IEntityTypeConfiguration<Badge>
{
    public void Configure(EntityTypeBuilder<Badge> e)
    {
        e.Property(x => x.Slug).HasMaxLength(80).IsRequired();
        e.HasIndex(x => x.Slug).IsUnique();
        e.Property(x => x.CriteriaJson).AsJson();
        e.Property(x => x.Description).AsProse();
    }
}

public class MemberBadgeConfig : IEntityTypeConfiguration<MemberBadge>
{
    public void Configure(EntityTypeBuilder<MemberBadge> e)
    {
        e.HasOne(x => x.Member).WithMany(x => x.Badges).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Badge).WithMany().HasForeignKey(x => x.BadgeId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.MemberId, x.BadgeId }).IsUnique();
    }
}

public class ChallengeConfig : IEntityTypeConfiguration<Challenge>
{
    public void Configure(EntityTypeBuilder<Challenge> e)
    {
        e.Property(x => x.Name).HasMaxLength(160).IsRequired();
        e.Property(x => x.Description).AsProse();
        e.Property(x => x.TargetValue).HasColumnType("decimal(10,2)");
        e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class FeedPostConfig : IEntityTypeConfiguration<FeedPost>
{
    public void Configure(EntityTypeBuilder<FeedPost> e)
    {
        e.Property(x => x.Title).HasMaxLength(240).IsRequired();
        e.Property(x => x.Body).AsProseNullable();
        e.Property(x => x.MetaJson).AsJsonNullable();
        e.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.SetNull);
        e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => new { x.IsVisible, x.PostedAtUtc });
    }
}

public class NotificationConfig : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> e)
    {
        e.Property(x => x.Title).HasMaxLength(240).IsRequired();
        e.Property(x => x.Body).AsProse();
        e.Property(x => x.PayloadJson).AsJsonNullable();
        e.HasOne(x => x.Member).WithMany(x => x.Notifications).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        e.HasIndex(x => new { x.MemberId, x.IsRead, x.CreatedAtUtc });
    }
}

public class CmsPageConfig : IEntityTypeConfiguration<CmsPage>
{
    public void Configure(EntityTypeBuilder<CmsPage> e)
    {
        e.Property(x => x.Slug).HasMaxLength(160).IsRequired();
        e.HasIndex(x => x.Slug).IsUnique();
        e.Property(x => x.Title).HasMaxLength(200).IsRequired();
        e.Property(x => x.SeoDescription).HasMaxLength(400);
        e.Property(x => x.Description).AsProseNullable();
        e.Property(x => x.StructuredDataJson).AsJsonNullable();
    }
}

public class CmsSectionConfig : IEntityTypeConfiguration<CmsSection>
{
    public void Configure(EntityTypeBuilder<CmsSection> e)
    {
        e.Property(x => x.Key).HasMaxLength(120).IsRequired();
        e.Property(x => x.AdminLabel).HasMaxLength(200).IsRequired();
        e.Property(x => x.ContentJson).AsJson();
        e.Property(x => x.DraftContentJson).AsJsonNullable();

        e.HasOne(x => x.CmsPage).WithMany(x => x.Sections).HasForeignKey(x => x.CmsPageId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => new { x.CmsPageId, x.OrderIndex });
        e.HasIndex(x => new { x.CmsPageId, x.Key, x.BranchId }).IsUnique();
    }
}

public class MediaAssetConfig : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> e)
    {
        e.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        e.Property(x => x.OriginalUrl).HasMaxLength(600).IsRequired();
        e.Property(x => x.VariantsJson).AsJson();
        e.Property(x => x.BlurDataUrl).AsProseNullable();
        e.Property(x => x.AltText).HasMaxLength(400);
        e.HasIndex(x => new { x.Kind, x.Folder });
    }
}

public class TestimonialConfig : IEntityTypeConfiguration<Testimonial>
{
    public void Configure(EntityTypeBuilder<Testimonial> e)
    {
        e.Property(x => x.AuthorName).HasMaxLength(160).IsRequired();
        e.Property(x => x.Quote).AsProse();
        e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => new { x.IsVisible, x.DisplayOrder });
    }
}

public class TransformationConfig : IEntityTypeConfiguration<Transformation>
{
    public void Configure(EntityTypeBuilder<Transformation> e)
    {
        e.Property(x => x.MemberDisplayName).HasMaxLength(160).IsRequired();
        e.Property(x => x.Story).AsProseNullable();
        e.Property(x => x.WeightBeforeKg).HasColumnType("decimal(5,1)");
        e.Property(x => x.WeightAfterKg).HasColumnType("decimal(5,1)");
        e.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.SetNull);
        e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => new { x.IsVisible, x.ConsentGiven, x.DisplayOrder });
    }
}

public class BlogPostConfig : IEntityTypeConfiguration<BlogPost>
{
    public void Configure(EntityTypeBuilder<BlogPost> e)
    {
        e.Property(x => x.Slug).HasMaxLength(200).IsRequired();
        e.HasIndex(x => x.Slug).IsUnique();
        e.Property(x => x.Title).HasMaxLength(240).IsRequired();
        e.Property(x => x.Excerpt).HasMaxLength(500);
        e.Property(x => x.BodyJson).AsJson();
        e.Property(x => x.SeoDescription).HasMaxLength(400);
        e.HasIndex(x => new { x.State, x.PublishedAtUtc });
    }
}

public class FaqItemConfig : IEntityTypeConfiguration<FaqItem>
{
    public void Configure(EntityTypeBuilder<FaqItem> e)
    {
        e.Property(x => x.Question).HasMaxLength(400).IsRequired();
        e.Property(x => x.Answer).AsProse();
        e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.SetNull);
        e.HasIndex(x => new { x.Category, x.DisplayOrder });
    }
}

public class SiteSettingConfig : IEntityTypeConfiguration<SiteSetting>
{
    public void Configure(EntityTypeBuilder<SiteSetting> e)
    {
        e.Property(x => x.Key).HasMaxLength(120).IsRequired();
        e.HasIndex(x => x.Key).IsUnique();
        e.Property(x => x.Value).AsProse();
        e.Property(x => x.HelpText).AsProseNullable();
    }
}
