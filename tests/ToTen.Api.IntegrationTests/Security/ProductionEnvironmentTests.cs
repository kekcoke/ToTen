using System.Net;
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
}
