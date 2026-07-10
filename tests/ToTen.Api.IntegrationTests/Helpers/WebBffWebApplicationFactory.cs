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
using ToTen.Api.Shared.Identity;
using ToTen.Api.Shared.Infrastructure;
using ToTen.Api.Shared.Messaging;

namespace ToTen.Api.IntegrationTests.Helpers;

/// <summary>
/// Unlike <see cref="ToTenWebApplicationFactory"/>, this factory leaves Program.cs's real
/// authentication registrations intact (the "smart" policy scheme, JwtBearer, and Cookies) —
/// the other factory fully replaces them with "TestScheme", which would erase the exact wiring
/// this feature needs to exercise. The only substituted seam is IKeycloakTokenClient, faked with
/// NSubstitute the same way IBus/IQRCodeService are substituted in the sibling factory, so tests
/// never make a real network call to Keycloak's token endpoint.
/// </summary>
public class WebBffWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:17-3.4")
        .Build();

    public IKeycloakTokenClient MockTokenClient { get; } = Substitute.For<IKeycloakTokenClient>();

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

    /// <summary>
    /// WebApplicationFactory's default client uses an http:// base address. The BFF's session
    /// cookie is Secure-only, so it would never be sent back on a plain http request even though
    /// this is all in-memory TestServer traffic — an https:// base address is required for the
    /// cookie round-trip to actually work in these tests.
    /// </summary>
    public HttpClient CreateWebClient(bool allowAutoRedirect = false)
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = allowAutoRedirect,
            HandleCookies = true,
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Authority"] = "https://test.authority.com/realms/ToTen",
                ["Auth:Audience"] = "test-audience",
                ["Auth:ApiScope"] = "test-scope",
                ["Auth:WebBff:ClientId"] = "ToTen-web-bff",
                ["Auth:WebBff:ClientSecret"] = "test-secret",
                ["Auth:WebBff:RedirectUri"] = "https://localhost/auth/callback",
                ["ConnectionStrings:ToTenDB"] = _db.GetConnectionString(),
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ToTenContext>();
            services.RemoveAll<DbContextOptions<ToTenContext>>();
            services.RemoveAll<IDbContextFactory<ToTenContext>>();
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

            services.RemoveAll<IHostedService>();
            services.RemoveAll<IBus>();
            services.AddSingleton(Substitute.For<IBus>());

            var mockQr = Substitute.For<IQRCodeService>();
            mockQr.GenerateAndSaveQRCodeAsync(Arg.Any<string>(), Arg.Any<string>())
                  .Returns("https://test.blob.core.windows.net/blobs/qr-test.png");
            services.AddSingleton(mockQr);

            services.RemoveAll<IKeycloakTokenClient>();
            services.AddSingleton(MockTokenClient);
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
