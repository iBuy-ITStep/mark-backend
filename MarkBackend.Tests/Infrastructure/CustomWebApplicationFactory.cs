using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MarkBackend.Data;
using Microsoft.AspNetCore.Identity;
using MarkBackend.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace MarkBackend.Tests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private static bool _seeded = false;
    private static readonly object _seedLock = new object();
    private const string DatabaseName = "TestDatabase_Shared";
    private const string TestJwtSecret = "TestSecretKeyForJwtTokenGenerationThatIsLongEnough123!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Add configuration FIRST
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["JwtSecret"] = TestJwtSecret,
                ["CorsOrigins"] = "*",
                // Add dummy SMTP settings so EmailHelper doesn't crash
                ["SmtpHost"] = "localhost",
                ["SmtpPort"] = "25",
                ["SmtpUser"] = "test@test.com",
                ["SmtpPassword"] = "testpassword",
                ["SmtpFrom"] = "test@test.com"
            }!);
        });

        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove all DbContext-related registrations
            var descriptorsToRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationContext>) ||
                           d.ServiceType == typeof(ApplicationContext) ||
                           d.ServiceType.ToString().Contains("DbContextOptions"))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            // Add InMemory database with shared name
            services.AddDbContext<ApplicationContext>(options =>
            {
                options.UseInMemoryDatabase(DatabaseName)
                       .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Reconfigure JWT Bearer authentication with the test secret
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var key = Encoding.ASCII.GetBytes(TestJwtSecret);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                };
            });

            // Seed data once after the service provider is built
            if (!_seeded)
            {
                lock (_seedLock)
                {
                    if (!_seeded)
                    {
                        var sp = services.BuildServiceProvider();
                        using var scope = sp.CreateScope();
                        var scopedServices = scope.ServiceProvider;
                        var db = scopedServices.GetRequiredService<ApplicationContext>();
                        var userManager = scopedServices.GetRequiredService<UserManager<User>>();
                        var roleManager = scopedServices.GetRequiredService<RoleManager<IdentityRole>>();

                        // Ensure database is created
                        db.Database.EnsureCreated();

                        // Seed test data
                        try
                        {
                            TestDataSeeder.SeedAsync(db, userManager, roleManager).Wait();
                            _seeded = true;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Seeding failed: {ex.Message}");
                            throw;
                        }
                    }
                }
            }
        });
    }
}
