using System.Security.Claims;

namespace ToTen.Api.Shared.Identity;

public class KeycloakIdentityManager : IIdentityManager
{
    public UserContext? GetCurrentUser(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true) return null;

        var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(idClaim, out var userId)) return null;

        var email = principal.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        
        // Custom claim for organization added during Phase 1 Keycloak setup
        var orgClaim = principal.FindFirst("organization_id")?.Value;
        Guid? orgId = Guid.TryParse(orgClaim, out var g) ? g : null;

        return new UserContext(userId, email, roles, orgId);
    }

    public bool IsInRole(ClaimsPrincipal principal, string role) => principal.IsInRole(role);

    public bool HasOrganizationAccess(ClaimsPrincipal principal, Guid organizationId)
    {
        var user = GetCurrentUser(principal);
        return user?.OrganizationId == organizationId || IsInRole(principal, "admin");
    }
}
