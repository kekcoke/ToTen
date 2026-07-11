using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using NSubstitute;
using ToTen.Api.Features.Auth;
using ToTen.Api.IntegrationTests.Helpers;
using ToTen.Api.Shared.Identity;

namespace ToTen.Api.IntegrationTests.Security;

/// <summary>
/// End-to-end coverage for the web BFF's Authorization Code + PKCE flow (audit finding 1.8).
/// Keycloak's token endpoint is faked via WebBffWebApplicationFactory.MockTokenClient — nothing
/// here calls a real Keycloak instance. Mobile's bearer path is unaffected by this feature (it
/// never calls anything under /auth) and is already covered by AuthorizationContractTests and
/// every feature's existing CreateAuthenticatedClient-based tests.
/// </summary>
public class WebBffAuthFlowTests(WebBffWebApplicationFactory factory) : IClassFixture<WebBffWebApplicationFactory>
{
    [Fact]
    public async Task Login_RedirectsToKeycloakAuthorizeEndpoint_WithPkceAndState()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateWebClient();

        var response = await client.GetAsync("/auth/login", ct);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.StartsWith("https://test.authority.com/realms/ToTen/protocol/openid-connect/auth", location.ToString());

        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("ToTen-web-bff", query["client_id"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrEmpty(query["code_challenge"]));
        Assert.False(string.IsNullOrEmpty(query["state"]));
    }

    [Fact]
    public async Task Callback_ValidCodeAndState_SetsSessionCookieAndRedirects()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateWebClient();
        var state = await ExtractStateFromLoginRedirectAsync(client, ct);
        ConfigureTokenExchange();

        var response = await client.GetAsync($"/auth/callback?code=test-code&state={state}", ct);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains(response.Headers, h => h.Key == "Set-Cookie" && h.Value.Any(v => v.Contains("__Host-ToTen-Session")));
    }

    [Fact]
    public async Task Callback_StateMismatch_Returns400()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateWebClient();
        await ExtractStateFromLoginRedirectAsync(client, ct);
        ConfigureTokenExchange();

        var response = await client.GetAsync("/auth/callback?code=test-code&state=wrong-state", ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithSessionCookie_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateWebClient();
        await LogInAsync(client, ct);

        var response = await client.GetAsync("/items", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Logout_ClearsSessionCookie_SubsequentRequestRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateWebClient();
        await LogInAsync(client, ct);

        // Logout is a mutating POST authenticated via the Cookies scheme, so it's subject to
        // the same CSRF check as any other mutating endpoint — fetch a token first.
        var csrfToken = (await client.GetFromJsonAsync<CsrfTokenResponse>("/auth/csrf", ct))!.Token;
        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        logoutRequest.Headers.Add("X-CSRF-Token", csrfToken);
        var logoutResponse = await client.SendAsync(logoutRequest, ct);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var response = await client.GetAsync("/items", ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TamperedCookie_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateWebClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/items");
        request.Headers.Add("Cookie", "__Host-ToTen-Session=tampered-value-not-a-real-ticket");

        var response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MutatingRequest_WithCookieAuth_NoCsrfHeader_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateWebClient();
        await LogInAsync(client, ct);

        var payload = new { name = "Test Item", description = "desc", categoryId = factory.GetSeedCategoryId() };
        var response = await client.PostAsJsonAsync("/items", payload, ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MutatingRequest_WithCookieAuth_WithCsrfHeader_IsAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = factory.CreateWebClient();
        await LogInAsync(client, ct);

        var csrfResponse = await client.GetFromJsonAsync<CsrfTokenResponse>("/auth/csrf", ct);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/items")
        {
            Content = JsonContent.Create(new { name = "Test Item", description = "desc", categoryId = factory.GetSeedCategoryId() }),
        };
        request.Headers.Add("X-CSRF-Token", csrfResponse!.Token);

        var response = await client.SendAsync(request, ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private void ConfigureTokenExchange()
    {
        var idToken = BuildIdToken(Guid.NewGuid().ToString(), "web-user@example.com", ["user"]);
        factory.MockTokenClient
            .ExchangeCodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new KeycloakTokenResponse("test-access-token", "test-refresh-token", idToken, 300));
    }

    private async Task LogInAsync(HttpClient client, CancellationToken ct)
    {
        var state = await ExtractStateFromLoginRedirectAsync(client, ct);
        ConfigureTokenExchange();

        var response = await client.GetAsync($"/auth/callback?code=test-code&state={state}", ct);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    private static async Task<string> ExtractStateFromLoginRedirectAsync(HttpClient client, CancellationToken ct)
    {
        var response = await client.GetAsync("/auth/login", ct);
        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        return query["state"]!;
    }

    private static string BuildIdToken(string userId, string email, string[] realmRoles)
    {
        var realmAccessJson = JsonSerializer.Serialize(new { roles = realmRoles });
        var claims = new List<Claim>
        {
            new("sub", userId),
            new("email", email),
            new("realm_access", realmAccessJson, System.IdentityModel.Tokens.Jwt.JsonClaimValueTypes.Json),
            new("resource_access", "{}", System.IdentityModel.Tokens.Jwt.JsonClaimValueTypes.Json),
        };

        var token = new JwtSecurityToken(
            issuer: "https://test.authority.com/realms/ToTen",
            audience: "test-audience",
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(5));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
