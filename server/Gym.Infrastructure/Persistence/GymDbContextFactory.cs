using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Gym.Infrastructure.Persistence;

/// <summary>
/// Design-time context for `dotnet ef`. Without this, the tools boot the API's host to find a
/// DbContext, which also executes the startup migrate-and-seed block — so simply generating a
/// migration would create and populate a database as a side effect. This keeps the tooling to
/// reading configuration and nothing else.
/// </summary>
public class GymDbContextFactory : IDesignTimeDbContextFactory<GymDbContext>
{
    public GymDbContext CreateDbContext(string[] args)
    {
        var apiProject = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "..", "Gym.Api"));
        var basePath = Directory.Exists(apiProject) ? apiProject : Directory.GetCurrentDirectory();

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("Default")
            ?? "Server=localhost\\SQLEXPRESS;Database=GymDb;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<GymDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(GymDbContext).Assembly.FullName))
            .Options;

        return new GymDbContext(options);
    }
}
