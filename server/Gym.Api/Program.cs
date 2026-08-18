using System.Text;
using Gym.Api.Contracts;
using Gym.Api.Hubs;
using Gym.Core.Entities;
using Gym.Infrastructure;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Persistence.Seeding;
using Gym.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog first, so startup failures are captured too.
builder.Host.UseSerilog((context, services, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

var webRoot = builder.Environment.WebRootPath
    ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(webRoot);

builder.Services.AddInfrastructure(builder.Configuration, webRoot, builder.Environment.IsDevelopment());

// ---------------------------------------------------------------- auth

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (jwt.SigningKey.Length < 32)
    throw new InvalidOperationException(
        "Jwt:SigningKey must be at least 32 characters. Set it in appsettings.Development.json or user secrets.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(15)
        };

        // SignalR cannot set an Authorization header, so it passes the token on the query string.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(RoleNames.Admin));
    options.AddPolicy("MemberOnly", policy => policy.RequireRole(RoleNames.Member));
    // Placeholder for the Phase-3 branch-scope handler; the claim is already issued.
    options.AddPolicy("BranchScoped", policy => policy.RequireAuthenticatedUser());
});

// ---------------------------------------------------------------- mvc, swagger, realtime

builder.Services
    .AddControllers()
    // The wall-clock times this API renders are "HH:mm"; the stock converter only reads
    // "HH:mm:ss", so without this the timetable builder cannot post back what it was shown.
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new TimeOnlyJsonConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddSignalR();
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FORGE API",
        Version = "v1",
        Description = "Multi-branch gym management, CMS and member platform."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token from /api/auth/login (no 'Bearer ' prefix)."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }]
            = Array.Empty<string>()
    });
});

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options => options.AddPolicy("client", policy => policy
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

// ---------------------------------------------------------------- migrate & seed

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GymDbContext>();
    var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
    {
        log.LogInformation("Applying EF Core migrations…");
        await db.Database.MigrateAsync();
    }

    if (app.Configuration.GetValue("Database:SeedOnStartup", true))
    {
        var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
        var result = await seeder.RunAsync(new SeedOptions
        {
            AdminEmail = app.Configuration["Seed:AdminEmail"] ?? "admin@gym.local",
            AdminPassword = app.Configuration["Seed:AdminPassword"],
            AdminFullName = app.Configuration["Seed:AdminFullName"] ?? "Owner"
        });

        if (result.AdminWasCreated)
            log.LogWarning(
                "\n" +
                "  ┌───────────────────────────────────────────────────────────────┐\n" +
                "  │  SEEDED CREDENTIALS — shown once, store them now              │\n" +
                "  ├───────────────────────────────────────────────────────────────┤\n" +
                "  │  Admin    {AdminEmail}\n" +
                "  │           {AdminPassword}\n" +
                "  │           (password change is forced on first login)          │\n" +
                "  │  Members  any seeded member email · {MemberPassword}\n" +
                "  └───────────────────────────────────────────────────────────────┘",
                result.AdminEmail, result.AdminPassword, result.MemberDemoPassword);
    }
}

// ---------------------------------------------------------------- pipeline

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "FORGE API v1");
        options.DocumentTitle = "FORGE API";
    });
}
else
{
    app.UseHttpsRedirection();
}

app.UseResponseCompression();

// Media is served straight from wwwroot with a long cache — filenames are content-stamped.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (context.Context.Request.Path.StartsWithSegments("/media"))
            context.Context.Response.Headers.CacheControl = "public,max-age=2592000,immutable";
    }
});

app.UseCors("client");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<OccupancyHub>(OccupancyHub.Route);
app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow })).WithTags("Health");

app.Run();
