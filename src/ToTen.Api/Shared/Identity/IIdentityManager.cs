using System.Security.Claims;

namespace ToTen.Api.Shared.Identity;

public interface IIdentityManager
{
    UserContext? GetCurrentUser(ClaimsPrincipal? principal);
    bool IsInRole(ClaimsPrincipal principal, string role);
    bool HasOrganizationAccess(ClaimsPrincipal principal, Guid organizationId);
}

public record UserContext(Guid Id, string Email, string[] Roles, Guid? OrganizationId);
