using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Rebus.Bus;
using Testcontainers.PostgreSql;
using ToTen.Api.Data;
using ToTen.Api.Models;
using ToTen.Api.Shared.Infrastructure;
using ToTen.Api.Shared.Messaging;

namespace ToTen.Api.IntegrationTests.Helpers;

public class ToTenWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:17-3.4")
        .Build();

    public Guid DefaultTestUserId { get; } = Guid.NewGuid();

    async ValueTask IAsyncLifetime.InitializeAsync() => await _db.StartAsync();

    public override async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }

    public ToTenContext CreateDbContext()
    {
        return Services.GetRequiredService<IDbContextFactory<ToTenContext>>().CreateDbContext();
    }

    public Guid GetSeedCategoryId()
    {
        using var ctx = CreateDbContext();
        return ctx.Categories.First().Id;
    }

    public HttpClient CreateAuthenticatedClient(
        string[]? roles = null,
        Guid? userId = null,
        Guid? orgId = null,
        string? email = null)
    {
        return WithAuth(succeeds: true, roles: roles, userId: userId, orgId: orgId, email: email)
            .CreateClient();
    }

    public HttpClient CreateUnauthenticatedClient()
    {
        return WithAuth(succeeds: false).CreateClient();
    }

    public WebApplicationFactory<Program> WithAuth(
        bool succeeds = true,
        string[]? roles = null,
        Guid? userId = null,
        Guid? orgId = null,
        string? email = null)
    {
        return WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Parent's ConfigureWebHost already registered "TestScheme" once.
                // PostConfigure overrides the options without re-adding the scheme,
                // preventing the "Scheme already exists" duplicate-registration error.
                services.PostConfigure<TestAuthOptions>("TestScheme", opts =>
                {
                    opts.AuthenticationSucceeds = succeeds;
                    opts.UserId = userId ?? DefaultTestUserId;
                    opts.Email = email ?? "test@example.com";
                    opts.Roles = roles ?? [];
                    opts.OrganizationId = orgId;
                });
            });
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Authority"]            = "https://test.authority.com",
                ["Auth:Audience"]             = "test-audience",
                ["Auth:ApiScope"]             = "test-scope",
                ["ConnectionStrings:ToTenDB"] = _db.GetConnectionString()
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove ALL DbContext registrations so Aspire's real-DB config doesn't leak in
            services.RemoveAll<ToTenContext>();
            services.RemoveAll<DbContextOptions<ToTenContext>>();
            services.RemoveAll<IDbContextFactory<ToTenContext>>();
            // Remove Aspire's options configuration — it validates ConnectionStrings:ToTenDB
            // against its own registered NpgsqlDataSource, bypassing our inline UseNpgsql call
            services.RemoveAll<IDbContextOptionsConfiguration<ToTenContext>>();

            var connectionString = _db.GetConnectionString();
            services.AddDbContextFactory<ToTenContext>(opts =>
                opts.UseNpgsql(connectionString, o => o.UseNetTopologySuite())
                    .UseAsyncSeeding(async (context, _, ct) =>
                    {
                        if (!context.Set<Category>().Any())
                        {
                            SeedCategories(context);
                            await context.SaveChangesAsync(ct);
                        }
                    }));

            services.RemoveAll<IEventPublisher>();
            services.AddScoped<IEventPublisher>(sp =>
                new MockEventPublisher(sp.GetRequiredService<ILogger<MockEventPublisher>>()));

            // Remove all hosted services (Rebus, OTEL, etc.) — not needed in the test server
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IBus>();
            services.AddSingleton(Substitute.For<IBus>());

            // Mock IQRCodeService so GenerateQR doesn't call Azure Blob Storage
            var mockQr = Substitute.For<IQRCodeService>();
            mockQr.GenerateAndSaveQRCodeAsync(Arg.Any<string>(), Arg.Any<string>())
                  .Returns("https://test.blob.core.windows.net/blobs/qr-test.png");
            services.AddSingleton(mockQr);

            services.RemoveAll<IAuthenticationSchemeProvider>();
            services.AddAuthentication("TestScheme")
                .AddScheme<TestAuthOptions, TestAuthHandler>("TestScheme", opts =>
                {
                    opts.AuthenticationSucceeds = true;
                    opts.UserId = DefaultTestUserId;
                    opts.Email = "test@example.com";
                    opts.Roles = [];
                });
        });

        builder.UseEnvironment(EnvironmentName);
    }

    protected virtual string EnvironmentName => "Test";

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
