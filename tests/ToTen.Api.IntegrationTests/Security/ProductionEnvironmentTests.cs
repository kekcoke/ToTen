using System.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ToTen.Api.IntegrationTests.Helpers;

namespace ToTen.Api.IntegrationTests.Security;

/// <summary>
/// Regression coverage for audit finding 1.1: production must not run with
/// ASPNETCORE_ENVIRONMENT=Development. That mode gated Swagger UI/OpenAPI exposure
/// and CORS AllowAnyOrigin via Program.cs's IsDevelopment() branch. These tests boot
/// the host in the "Production" environment (rather than the fixture's default "Test")
/// to assert the code path itself is safe, independent of what Terraform happens to set.
/// </summary>
public class ProductionEnvironmentTests : IAsyncLifetime
{
    private sealed class ProductionWebApplicationFactory : ToTenWebApplicationFactory
    {
        protected override string EnvironmentName => "Production";
    }

    private readonly ProductionWebApplicationFactory _factory = new();
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
    public async Task SwaggerUI_InProduction_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/swagger/index.html", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiSchema_InProduction_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cors_InProduction_DoesNotAllowArbitraryOrigin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/organizations/" + Guid.NewGuid());
        request.Headers.Add("Origin", "https://evil.example.com");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    /// <summary>
    /// Regression coverage for audit finding 3.1: RequireHttpsMetadata must not be
    /// hardcoded to false — it should be environment-gated the same way CORS is.
    /// </summary>
    [Fact]
    public void JwtBearer_InProduction_RequiresHttpsMetadata()
    {
        var options = _factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.True(options.RequireHttpsMetadata);
    }
}

/// <summary>
/// Companion to <see cref="ProductionEnvironmentTests.JwtBearer_InProduction_RequiresHttpsMetadata"/>:
/// confirms the Development branch of the same gate still allows plain HTTP metadata,
/// so local/dev workflows (self-signed certs, HTTP-only Keycloak) keep working.
/// </summary>
public class DevelopmentEnvironmentJwtBearerTests : IAsyncLifetime
{
    private sealed class DevelopmentWebApplicationFactory : ToTenWebApplicationFactory
    {
        protected override string EnvironmentName => "Development";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SWAGGERUI_CLIENTID"] = "test-swagger-client"
                });
            });
        }
    }

    private readonly DevelopmentWebApplicationFactory _factory = new();

    public async ValueTask InitializeAsync() => await ((IAsyncLifetime)_factory).InitializeAsync();

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public void JwtBearer_InDevelopment_AllowsHttpMetadata()
    {
        var options = _factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.False(options.RequireHttpsMetadata);
    }
}
