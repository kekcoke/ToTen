using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ToTen.Api.Shared.Authentication;

/// <summary>
/// Flattens Keycloak's native JWT claim shape (raw "sub"/"email", nested
/// "realm_access"/"resource_access" role objects) into the long-form
/// ClaimTypes.* claims the rest of the app reads, since JwtBearerOptionsSetup
/// sets MapInboundClaims = false and Keycloak never emits those URIs itself.
/// </summary>
public class KeycloakClaimsTransformation(IOptions<AuthOptions> authOptions) : IClaimsTransformation
{
    private const string TransformedMarker = "toten_claims_transformed";

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = principal.Identity as ClaimsIdentity;
        if (identity is null || !identity.IsAuthenticated || identity.HasClaim(c => c.Type == TransformedMarker))
        {
            return Task.FromResult(principal);
        }

        identity.AddClaim(new Claim(TransformedMarker, "true"));

        if (identity.FindFirst(ClaimTypes.NameIdentifier) is null &&
            identity.FindFirst("sub") is { } sub)
        {
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, sub.Value));
        }

        if (identity.FindFirst(ClaimTypes.Email) is null &&
            identity.FindFirst("email") is { } email)
        {
            identity.AddClaim(new Claim(ClaimTypes.Email, email.Value));
        }

        foreach (var role in ExtractRoles(identity).Where(role => !identity.HasClaim(ClaimTypes.Role, role)))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        return Task.FromResult(principal);
    }

    private IEnumerable<string> ExtractRoles(ClaimsIdentity identity)
    {
        if (identity.FindFirst("realm_access") is { } realmAccess)
        {
            foreach (var role in ExtractRolesArray(realmAccess.Value))
            {
                yield return role;
            }
        }

        if (identity.FindFirst("resource_access") is { } resourceAccess)
        {
            foreach (var role in ExtractClientRoles(resourceAccess.Value, authOptions.Value.Audience))
            {
                yield return role;
            }
        }
    }

    private static IEnumerable<string> ExtractRolesArray(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Array)
        {
            foreach (var role in roles.EnumerateArray())
            {
                if (role.GetString() is { } value)
                {
                    yield return value;
                }
            }
        }
    }

    private static IEnumerable<string> ExtractClientRoles(string json, string clientId)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty(clientId, out var client) &&
            client.TryGetProperty("roles", out var roles) &&
            roles.ValueKind == JsonValueKind.Array)
        {
            foreach (var role in roles.EnumerateArray())
            {
                if (role.GetString() is { } value)
                {
                    yield return value;
                }
            }
        }
    }
}
