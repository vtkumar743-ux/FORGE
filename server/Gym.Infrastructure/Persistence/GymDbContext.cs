using Gym.Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence;

public class GymDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, int>
{
    public GymDbContext(DbContextOptions<GymDbContext> options) : base(options) { }

    // Identity extras
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Gym ops
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Trainer> Trainers => Set<Trainer>();
    public DbSet<TrainerRating> TrainerRatings => Set<TrainerRating>();

    // Billing
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanBranchPrice> PlanBranchPrices => Set<PlanBranchPrice>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<FreezeRequest> FreezeRequests => Set<FreezeRequest>();

    // Scheduling
    public DbSet<ClassFormat> ClassFormats => Set<ClassFormat>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<ClassSchedule> ClassSchedules => Set<ClassSchedule>();
    public DbSet<ClassSession> ClassSessions => Set<ClassSession>();
    public DbSet<Booking> Bookings => Set<Booking>();

    // Attendance & CRM
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<LeadFollowUp> LeadFollowUps => Set<LeadFollowUp>();

    // Training
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<WorkoutProgram> WorkoutPrograms => Set<WorkoutProgram>();
    public DbSet<ProgramDay> ProgramDays => Set<ProgramDay>();
    public DbSet<ProgramExercise> ProgramExercises => Set<ProgramExercise>();
    public DbSet<WorkoutLog> WorkoutLogs => Set<WorkoutLog>();
    public DbSet<DietPlan> DietPlans => Set<DietPlan>();
    public DbSet<Meal> Meals => Set<Meal>();
    public DbSet<BodyScan> BodyScans => Set<BodyScan>();
    public DbSet<ProgressPhoto> ProgressPhotos => Set<ProgressPhoto>();

    // Commerce
    public DbSet<Product> Products => Set<Product>();
    public DbSet<BranchStock> BranchStocks => Set<BranchStock>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    // Engagement
    public DbSet<Referral> Referrals => Set<Referral>();
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<MemberBadge> MemberBadges => Set<MemberBadge>();
    public DbSet<Challenge> Challenges => Set<Challenge>();
    public DbSet<FeedPost> FeedPosts => Set<FeedPost>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // Corporate (Module 4.6)
    public DbSet<CorporateAccount> CorporateAccounts => Set<CorporateAccount>();
    public DbSet<CorporateEnrolment> CorporateEnrolments => Set<CorporateEnrolment>();

    // CMS
    public DbSet<CmsPage> CmsPages => Set<CmsPage>();
    public DbSet<CmsSection> CmsSections => Set<CmsSection>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<Testimonial> Testimonials => Set<Testimonial>();
    public DbSet<Transformation> Transformations => Set<Transformation>();
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
    public DbSet<FaqItem> FaqItems => Set<FaqItem>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.HasDefaultSchema("gym");
        foreach (var entity in b.Model.GetEntityTypes().Where(e => e.ClrType.Name.StartsWith("Identity")))
            entity.SetSchema("auth");

        b.ApplyConfigurationsFromAssembly(typeof(GymDbContext).Assembly);

        // Money: SQL Server defaults decimal to (18,2) with a truncation warning; be explicit once.
        foreach (var property in b.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            if (property.GetColumnType() is null)
                property.SetColumnType("decimal(18,2)");
        }

        // Every string column would otherwise be nvarchar(max); cap the default.
        foreach (var property in b.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(string)))
        {
            if (property.GetMaxLength() is null && property.GetColumnType() is null)
                property.SetMaxLength(512);
        }
    }

    public override int SaveChanges()
    {
        StampAudit();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAudit();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampAudit()
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAtUtc == default)
                entry.Entity.CreatedAtUtc = DateTime.UtcNow;
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}

/// <summary>Shared long-text helper so JSON/prose columns opt out of the 512-char default.</summary>
internal static class BuilderExtensions
{
    public static PropertyBuilder<string> AsJson(this PropertyBuilder<string> p) => p.HasColumnType("nvarchar(max)");
    public static PropertyBuilder<string?> AsJsonNullable(this PropertyBuilder<string?> p) => p.HasColumnType("nvarchar(max)");
    public static PropertyBuilder<string> AsProse(this PropertyBuilder<string> p) => p.HasColumnType("nvarchar(max)");
    public static PropertyBuilder<string?> AsProseNullable(this PropertyBuilder<string?> p) => p.HasColumnType("nvarchar(max)");
}
