using Gym.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

public class PlanConfig : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> e)
    {
        e.Property(x => x.Name).HasMaxLength(120).IsRequired();
        e.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        e.HasIndex(x => x.Slug).IsUnique();
        e.Property(x => x.Description).AsProse();
        e.Property(x => x.FeaturesJson).AsJson();
        e.Property(x => x.GstRatePercent).HasColumnType("decimal(5,2)");
        e.Property(x => x.SacCode).HasMaxLength(12);
        e.HasIndex(x => new { x.ShowOnWebsite, x.DisplayOrder });
    }
}

public class PlanBranchPriceConfig : IEntityTypeConfiguration<PlanBranchPrice>
{
    public void Configure(EntityTypeBuilder<PlanBranchPrice> e)
    {
        e.HasOne(x => x.Plan).WithMany(x => x.BranchPrices).HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Branch).WithMany(x => x.PlanPrices).HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(x => new { x.PlanId, x.BranchId }).IsUnique();
    }
}

public class SubscriptionConfig : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> e)
    {
        e.HasOne(x => x.Member).WithMany(x => x.Subscriptions).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Plan).WithMany(x => x.Subscriptions).HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Coupon).WithMany().HasForeignKey(x => x.CouponId).OnDelete(DeleteBehavior.SetNull);
        e.HasOne(x => x.UpgradedFromSubscription).WithMany().HasForeignKey(x => x.UpgradedFromSubscriptionId).OnDelete(DeleteBehavior.NoAction);

        // Renewals/expiring-in-7-days dashboards read this pair constantly.
        e.HasIndex(x => new { x.Status, x.EndsOn });
        e.HasIndex(x => new { x.MemberId, x.Status });
    }
}

public class CouponConfig : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> e)
    {
        e.Property(x => x.Code).HasMaxLength(40).IsRequired();
        e.HasIndex(x => x.Code).IsUnique();
        e.Property(x => x.DiscountValue).HasColumnType("decimal(10,2)");
        e.Property(x => x.Description).AsProseNullable();
    }
}

public class InvoiceConfig : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> e)
    {
        e.Property(x => x.InvoiceNumber).HasMaxLength(40).IsRequired();
        e.HasIndex(x => x.InvoiceNumber).IsUnique();
        e.Property(x => x.Notes).AsProseNullable();

        e.HasOne(x => x.Member).WithMany(x => x.Invoices).HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        // Financial rows are never deleted with their subject — the payment audit trail must
        // survive member/subscription removal (non-functional requirement: non-repudiable).
        e.HasOne(x => x.Subscription).WithMany(x => x.Invoices).HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction);

        // Outstanding-dues and dunning sweeps.
        e.HasIndex(x => new { x.Status, x.DueOn });
        e.HasIndex(x => new { x.BranchId, x.IssuedOn });
    }
}

public class InvoiceLineConfig : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> e)
    {
        e.Property(x => x.Description).HasMaxLength(300).IsRequired();
        e.Property(x => x.GstRatePercent).HasColumnType("decimal(5,2)");
        e.Property(x => x.SacOrHsnCode).HasMaxLength(12);
        e.HasOne(x => x.Invoice).WithMany(x => x.Lines).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PaymentConfig : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> e)
    {
        e.HasOne(x => x.Invoice).WithMany(x => x.Payments).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);

        e.Property(x => x.GatewayPayloadJson).AsJsonNullable();
        e.Property(x => x.Notes).AsProseNullable();
        e.Property(x => x.IdempotencyKey).HasMaxLength(80);
        // Webhook retries must not double-credit a payment.
        e.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        e.HasIndex(x => x.GatewayPaymentId);
        e.HasIndex(x => new { x.BranchId, x.PaidAtUtc });
    }
}

public class FreezeRequestConfig : IEntityTypeConfiguration<FreezeRequest>
{
    public void Configure(EntityTypeBuilder<FreezeRequest> e)
    {
        e.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Subscription).WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.NoAction);

        e.Property(x => x.Reason).HasMaxLength(400).IsRequired();
        e.Property(x => x.DecisionNote).AsProseNullable();
        e.Ignore(x => x.Days);

        // The desk's pending queue, and the member's own history.
        e.HasIndex(x => new { x.Status, x.RequestedAtUtc });
        e.HasIndex(x => new { x.MemberId, x.Status });
    }
}
