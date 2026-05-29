using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ToTen.Api.Data;
using ToTen.Api.Models;
using ToTen.Api.Shared.Identity;

namespace ToTen.Api.Shared.Authorization;

public class ResourceAuthorizationHandler : AuthorizationHandler<ResourceOwnerRequirement, object>
{
    private readonly IIdentityManager _identityManager;
    private readonly ToTenContext _dbContext;

    public ResourceAuthorizationHandler(IIdentityManager identityManager, ToTenContext dbContext)
    {
        _identityManager = identityManager;
        _dbContext = dbContext;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ResourceOwnerRequirement requirement,
        object resource)
    {
        var user = _identityManager.GetCurrentUser(context.User);
        if (user == null) return;

        // Super admins bypass ownership checks
        if (user.Roles.Contains("super_admin") || user.Roles.Contains("admin"))
        {
            context.Succeed(requirement);
            return;
        }

        bool hasAccess = resource switch
        {
            InventoryItem item => await CheckItemAccess(user, item),
            Location loc => await CheckLocationAccess(user, loc),
            Box box => await CheckBoxAccess(user, box),
            _ => false
        };

        if (hasAccess)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> CheckItemAccess(UserContext user, InventoryItem item)
    {
        var userIdString = user.Id.ToString();
        if (item.OwnerId == userIdString) return true;
        if (item.OrganizationId.HasValue)
        {
            return await _dbContext.OrganizationMemberships
                .AnyAsync(m => m.OrganizationId == item.OrganizationId && m.UserId == userIdString);
        }
        return false;
    }

    private async Task<bool> CheckLocationAccess(UserContext user, Location loc)
    {
        var userIdString = user.Id.ToString();
        if (loc.OwnerId == userIdString) return true;
        if (loc.OrganizationId.HasValue)
        {
            return await _dbContext.OrganizationMemberships
                .AnyAsync(m => m.OrganizationId == loc.OrganizationId && m.UserId == userIdString);
        }
        return false;
    }

    private async Task<bool> CheckBoxAccess(UserContext user, Box box)
    {
        var userIdString = user.Id.ToString();
        if (box.OwnerId == userIdString) return true;
        if (box.OrganizationId.HasValue)
        {
            return await _dbContext.OrganizationMemberships
                .AnyAsync(m => m.OrganizationId == box.OrganizationId && m.UserId == userIdString);
        }
        return false;
    }
}
