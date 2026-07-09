using System.Net;
using System.Net.Http.Json;
using ToTen.Api.IntegrationTests.Helpers;

namespace ToTen.Api.IntegrationTests.Security;

/// <summary>
/// Security contract tests — each protected route must return 401 for unauthenticated requests
/// and 403 for authenticated requests that lack the required role/policy.
/// </summary>
public class AuthorizationContractTests(ToTenWebApplicationFactory factory)
    : IClassFixture<ToTenWebApplicationFactory>
{
    public static IEnumerable<object[]> ProtectedRoutes()
    {
        // Manifests
        yield return ["POST", "/api/manifests"];
        // Storage
        yield return ["POST", "/api/locations"];
        yield return ["POST", $"/api/items/{Guid.NewGuid()}/move"];
        // Items
        yield return ["GET", "/items"];
        yield return ["GET", $"/items/{Guid.NewGuid()}"];
        yield return ["POST", "/items"];
        yield return ["PUT", $"/items/{Guid.NewGuid()}"];
        yield return ["DELETE", $"/items/{Guid.NewGuid()}"];
        // Organizations
        yield return ["POST", "/api/organizations"];
        yield return ["GET", $"/api/organizations/{Guid.NewGuid()}"];
        yield return ["DELETE", $"/api/organizations/{Guid.NewGuid()}"];
        // Users (AdminPolicy)
        yield return ["GET", "/api/users"];
        yield return ["PUT", $"/api/users/{Guid.NewGuid()}/roles"];
        // Memberships
        yield return ["POST", $"/api/organizations/{Guid.NewGuid()}/members"];
        // Marketplace
        yield return ["POST", "/api/listings"];
        yield return ["POST", $"/api/listings/{Guid.NewGuid()}/offers"];
        yield return ["POST", $"/api/offers/{Guid.NewGuid()}/accept"];
        // Boxes / Manifests
        yield return ["POST", $"/api/boxes/{Guid.NewGuid()}/qr"];
        yield return ["POST", $"/api/manifests/{Guid.NewGuid()}/boxes"];
    }

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task ProtectedRoute_UnauthenticatedRequest_Returns401(string method, string path)
    {
        var client = factory.CreateUnauthenticatedClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method is "POST" or "PUT"
                ? JsonContent.Create(new { })
                : null
        };

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public static IEnumerable<object[]> NonAdminRoles()
    {
        yield return [new[] { "user" }];
        yield return [new[] { "business_owner" }];
        yield return [new[] { "third_party" }];
    }

    [Theory]
    [MemberData(nameof(NonAdminRoles))]
    public async Task GetUsers_WithNonAdminRole_ReturnsForbidden(string[] roles)
    {
        var client = factory.CreateAuthenticatedClient(roles: roles);

        var response = await client.GetAsync("/api/users", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
