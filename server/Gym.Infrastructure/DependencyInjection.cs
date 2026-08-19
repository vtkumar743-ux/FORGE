using Gym.Core.Entities;
using Gym.Core.Interfaces;
using Gym.Infrastructure.BackgroundJobs;
using Gym.Infrastructure.Media;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Persistence.Seeding;
using Gym.Infrastructure.Services;
using Gym.Infrastructure.Services.PlanGeneration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Gym.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config, string webRootPath,
        bool isDevelopment = false)
    {
        var connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

        services.AddDbContext<GymDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(GymDbContext).Assembly.FullName);
                // SQL Express restarts and transient network blips should not surface as 500s.
                sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(3), null);
                sql.CommandTimeout(120);
            });
        });

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 8;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<GymDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<DbSeeder>();

        var mediaOptions = config.GetSection(MediaStorageOptions.SectionName).Get<MediaStorageOptions>()
            ?? new MediaStorageOptions();
        // Owner uploads live under wwwroot/media/uploads and are served at /media/uploads,
        // which keeps them from colliding with the build-time brand imagery the client ships.
        if (string.IsNullOrWhiteSpace(mediaOptions.RootPath))
            mediaOptions.RootPath = Path.Combine(webRootPath, "media", "uploads");
        services.AddSingleton(mediaOptions);
        services.AddSingleton<IMediaStorage>(sp => new LocalMediaStorage(
            mediaOptions, sp.GetRequiredService<ILogger<LocalMediaStorage>>()));

        // Progress photos are body photographs: they are stored a level above wwwroot, where no
        // static-file middleware is mapped, and streamed by an endpoint that checks the owner.
        var privateOptions = new MediaStorageOptions
        {
            RootPath = Path.GetFullPath(Path.Combine(webRootPath, "..", "private-media")),
            PublicBasePath = "/private-media",
            Widths = new[] { 640, 1080 },
            WebpQuality = mediaOptions.WebpQuality
        };
        services.AddSingleton(sp => new PrivateMediaStorage(
            privateOptions, sp.GetRequiredService<ILogger<LocalMediaStorage>>()));

        // ------------------------------------------------------------------ module 2

        var razorpay = config.GetSection(RazorpayOptions.SectionName).Get<RazorpayOptions>() ?? new RazorpayOptions();
        // The simulator only ever stands in during development; in any other environment an
        // unconfigured gateway refuses to transact rather than inventing a captured payment.
        razorpay.AllowSimulator = isDevelopment;
        services.AddSingleton(razorpay);
        services.AddHttpClient("razorpay", client => client.Timeout = TimeSpan.FromSeconds(20));
        services.AddScoped<IPaymentGateway, RazorpayGateway>();
        services.AddScoped<PaymentOrderService>();

        var messaging = config.GetSection(MessagingOptions.SectionName).Get<MessagingOptions>() ?? new MessagingOptions();
        services.AddSingleton(messaging);
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

        services.AddScoped<InvoiceService>();
        services.AddScoped<SubscriptionService>();
        services.AddScoped<SchedulingService>();

        // ------------------------------------------------------------------ module 3

        services.AddScoped<TrainingService>();

        // ------------------------------------------------------------------ module 4

        // The live meter's push seam. The API replaces this with the SignalR implementation;
        // design-time tooling and the background workers keep working without it.
        services.AddSingleton<IRealtimeNotifier, NullRealtimeNotifier>();
        services.AddScoped<OccupancyService>();
        services.AddScoped<ChurnService>();
        services.AddScoped<OfferService>();
        services.AddScoped<CorporateService>();
        services.AddScoped<ProgressReportService>();

        var claude = config.GetSection(ClaudeOptions.SectionName).Get<ClaudeOptions>() ?? new ClaudeOptions();
        services.AddSingleton(claude);
        services.AddSingleton<RuleBasedPlanGenerator>();
        services.AddSingleton<ClaudePlanGenerator>();
        // The rule-based generator is the one registered against the interface: it is the
        // path that always works. The service layer reaches for Claude first when a key is
        // configured and falls back to this without the caller knowing.
        services.AddSingleton<IPlanGenerator>(sp => sp.GetRequiredService<RuleBasedPlanGenerator>());
        services.AddScoped<PlanGenerationService>();

        // QuestPDF Community licence: free below the vendor's revenue threshold. Flagged for
        // the client in PROGRESS.md — worth confirming before launch.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        services.AddHostedService<DunningWorker>();
        services.AddHostedService<OperationsWorker>();

        return services;
    }
}
