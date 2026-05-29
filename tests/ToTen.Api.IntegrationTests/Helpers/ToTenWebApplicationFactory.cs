using DotNet.Testcontainers.Containers;
using ToTen.Api.Data;
using ToTen.Api.Models;
using ToTen.Api.Shared.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;

namespace ToTen.Api.IntegrationTests.Helpers;

public class ToTenWebApplicationFactory(
    IDatabaseContainer dbContainer,
    bool authenticationSucceeds = true,
    string? email = null
)
    : WebApplicationFactory<Program>
{
    public ToTenContext CreateDbContext()
    {
        var db = Services.GetRequiredService<IDbContextFactory<ToTenContext>>()
                                      .CreateDbContext();
        return db;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add test configuration for Auth options
            var testSettings = new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "https://test.authority.com",
                ["Auth:Audience"] = "test-audience",
                ["Auth:ApiScope"] = "test-scope"
            };
            config.AddInMemoryCollection(testSettings);
        });

        builder.ConfigureServices(services =>
        {
            // Configure the DB Context
            services.RemoveAll<DbContextOptions<ToTenContext>>();

            var dbContextOptionsBuilder = new DbContextOptionsBuilder<ToTenContext>();
            dbContextOptionsBuilder.UseNpgsql(dbContainer.GetConnectionString())
                .UseAsyncSeeding(async (context, _, cancellationToken) =>
                {
                    if (!context.Set<Category>().Any())
                    {
                        SeedCategories(context);
                        await context.SaveChangesAsync(cancellationToken);
                    }
                });

            services.AddSingleton(dbContextOptionsBuilder.Options);

            services.AddDbContextFactory<ToTenContext>();

            // Replace EventPublisher with mock for testing
            services.RemoveAll<IEventPublisher>();
            services.AddScoped<IEventPublisher>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<MockEventPublisher>>();
                return new MockEventPublisher(logger);
            });

            // Remove the existing authentication and add test authentication
            services.RemoveAll<IAuthenticationSchemeProvider>();
            services.AddAuthentication(defaultScheme: "TestScheme")
                    .AddScheme<TestAuthOptions, TestAuthHandler>(
                        "TestScheme",
                         options =>
                         {
                             options.AuthenticationSucceeds = authenticationSucceeds;
                             options.Email = email;
                         });
        });

        builder.UseEnvironment("Test");
    }

    private static void SeedCategories(DbContext context)
    {
        context.Set<Category>().AddRange(
            new Category { Name = "General" },
            new Category { Name = "Urgent" },
            new Category { Name = "Archived" },
            new Category { Name = "Favorites" },
            new Category { Name = "Upcoming" }
        );
    }
}

