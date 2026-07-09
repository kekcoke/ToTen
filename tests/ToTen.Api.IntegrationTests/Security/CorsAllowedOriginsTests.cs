using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using ToTen.Api.IntegrationTests.Helpers;
using System.Linq;

namespace ToTen.Api.IntegrationTests.Security;

/// <summary>
/// Regression coverage for audit finding 3.3: <see cref="ProductionEnvironmentTests.Cors_InProduction_DoesNotAllowArbitraryOrigin"/>
/// passes vacuously — no AllowedOrigins value is ever configured there, so any origin
/// (legitimate or not) resolves to an empty allowlist and gets rejected. These tests
/// configure a real AllowedOrigins value to prove a configured origin is actually
/// allowed, and an unconfigured one is still rejected.
/// </summary>
public class CorsAllowedOriginsTests : IAsyncLifetime
{
    private const string ConfiguredOrigin = "https://app.toten.example";

    private sealed class ProductionWithAllowedOriginsFactory : ToTenWebApplicationFactory
    {
        protected override string EnvironmentName => "Production";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AllowedOrigins"] = ConfiguredOrigin
                });
            });
        }
    }

    private readonly ProductionWithAllowedOriginsFactory _factory = new();
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        await ((IAsyncLifetime)_factory).InitializeAsync();
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Cors_InProduction_AllowsConfiguredOrigin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/organizations/" + Guid.NewGuid());
        request.Headers.Add("Origin", ConfiguredOrigin);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        Assert.Equal(ConfiguredOrigin, values!.Single());
    }

    [Fact]
    public async Task Cors_InProduction_RejectsNonConfiguredOrigin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/organizations/" + Guid.NewGuid());
        request.Headers.Add("Origin", "https://evil.example.com");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
