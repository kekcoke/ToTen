using System.Security.Claims;
using Microsoft.Extensions.Options;
using ToTen.Api.Shared.Authentication;
using ToTen.Api.Shared.Identity;

namespace ToTen.Api.IntegrationTests.Security;

/// <summary>
/// Verifies KeycloakClaimsTransformation flattens the exact claim shape a real Keycloak-issued
/// token has (captured from a live local Keycloak instance while diagnosing audit finding 2.2):
/// raw "sub"/"email" claims and nested "realm_access"/"resource_access" JSON role objects, none
/// of which map to ClaimTypes.* automatically once JwtBearerOptionsSetup sets MapInboundClaims = false.
/// </summary>
public class KeycloakClaimsTransformationTests
{
    private const string UserId = "e6b439d2-9a13-4d33-9750-6b5512d7a730";
    private const string RealmAccessJson = """{"roles":["default-roles-ToTen","offline_access","uma_authorization","user"]}""";
    private const string ResourceAccessJson = """{"account":{"roles":["manage-account","manage-account-links","view-profile"]},"ToTen-api":{"roles":["admin"]}}""";

    private static ClaimsPrincipal BuildKeycloakPrincipal()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", UserId),
            new Claim("email", "demo@example.com"),
            new Claim("realm_access", RealmAccessJson),
            new Claim("resource_access", ResourceAccessJson),
        ], "TestJwtScheme");

        return new ClaimsPrincipal(identity);
    }

    private static KeycloakClaimsTransformation CreateTransformation(string audience = "ToTen-api") =>
        new(Options.Create(new AuthOptions { Authority = "http://keycloak", Audience = audience, ApiScope = "ToTen_api.all" }));

    [Fact]
    public async Task TransformAsync_MapsSubToNameIdentifier()
    {
        var result = await CreateTransformation().TransformAsync(BuildKeycloakPrincipal());

        Assert.Equal(UserId, result.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public async Task TransformAsync_MapsEmailToClaimTypesEmail()
    {
        var result = await CreateTransformation().TransformAsync(BuildKeycloakPrincipal());

        Assert.Equal("demo@example.com", result.FindFirst(ClaimTypes.Email)?.Value);
    }

    [Fact]
    public async Task TransformAsync_FlattensRealmAccessRolesIntoClaimTypesRole()
    {
        var result = await CreateTransformation().TransformAsync(BuildKeycloakPrincipal());

        var roles = result.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();

        Assert.Contains("user", roles);
        Assert.Contains("offline_access", roles);
    }

    [Fact]
    public async Task TransformAsync_FlattensResourceAccessRolesForConfiguredAudience()
    {
        var result = await CreateTransformation(audience: "ToTen-api").TransformAsync(BuildKeycloakPrincipal());

        var roles = result.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();

        Assert.Contains("admin", roles);
    }

    [Fact]
    public async Task TransformAsync_IsIdempotent_DoesNotDuplicateRoleClaims()
    {
        var transformation = CreateTransformation();
        var principal = BuildKeycloakPrincipal();

        var first = await transformation.TransformAsync(principal);
        var second = await transformation.TransformAsync(first);

        var userRoleCount = second.FindAll(ClaimTypes.Role).Count(c => c.Value == "user");
        Assert.Equal(1, userRoleCount);
    }

    [Fact]
    public async Task TransformAsync_ThenKeycloakIdentityManager_ResolvesRealUser()
    {
        var result = await CreateTransformation().TransformAsync(BuildKeycloakPrincipal());

        var user = new KeycloakIdentityManager().GetCurrentUser(result);

        Assert.NotNull(user);
        Assert.Equal(Guid.Parse(UserId), user!.Id);
        Assert.Equal("demo@example.com", user.Email);
        Assert.Contains("user", user.Roles);
    }
}
