using Plannivo.Repositories.Persistence.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Plannivo.IntegrationTests;

/// <summary>
/// WebApplicationFactory that replaces the real PostgreSQL database with
/// a per-test-run in-memory database, so integration tests can run
/// without a running PostgreSQL instance.
/// </summary>
public class PlannivoWebApplicationFactory : WebApplicationFactory<Program>
{
    // Fixed name for the entire test-run lifetime of this factory instance.
    // Must be captured here, NOT inside the options lambda — if Guid.NewGuid()
    // were evaluated inside the lambda it would generate a different database
    // name on every DbContext instantiation (i.e. every HTTP request), meaning
    // each request would see a completely empty, isolated InMemory database.
    private readonly string _testDbName = $"plannivo-test-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = "testing-secret-key-for-integration-tests-min-32-chars",
                ["JwtSettings:Issuer"] = "Plannivo",
                ["JwtSettings:Audience"] = "PlannivoUsers",
                ["JwtSettings:AccessTokenExpiryMinutes"] = "15",
                ["JwtSettings:RefreshTokenExpiryDays"] = "7",
                ["Cors:AllowedOrigins:0"] = "http://localhost:4200"
            });
        });

        builder.ConfigureServices(services =>
        {
            // EF Core 8+ stores provider config in IDbContextOptionsConfiguration<T>.
            // Remove ALL ApplicationDbContext-related registrations so we can replace
            // the Npgsql provider with an isolated in-memory database.
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(ApplicationDbContext) ||
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>) &&
                     d.ServiceType.GenericTypeArguments[0] == typeof(ApplicationDbContext)))
                .ToList();

            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            // Register a fresh, isolated in-memory database for each factory instance.
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_testDbName));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // EnsureCreated() is called here (after the host is fully built and Serilog is frozen)
        // so HasData seeds (Roles) are applied to the InMemory database.
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();

        return host;
    }
}
