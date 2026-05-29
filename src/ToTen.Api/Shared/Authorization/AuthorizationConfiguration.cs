using Microsoft.AspNetCore.Authorization;
using ToTen.Api.Shared.Identity;

namespace ToTen.Api.Shared.Authorization;

public static class AuthorizationConfiguration
{
    public const string UserPolicy = "user";
    public const string BusinessOwnerPolicy = "business_owner";
    public const string InternalUserPolicy = "internal_user";
    public const string AdminPolicy = "admin";
    public const string SuperAdminPolicy = "super_admin";
    public const string ThirdPartyPolicy = "third_party";

    public static IServiceCollection AddToTenAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(UserPolicy, policy => policy.RequireRole("user", "admin", "super_admin"));
            options.AddPolicy(BusinessOwnerPolicy, policy => policy.RequireRole("business_owner", "admin", "super_admin"));
            options.AddPolicy(InternalUserPolicy, policy => policy.RequireRole("internal_user", "admin", "super_admin"));
            options.AddPolicy(AdminPolicy, policy => policy.RequireRole("admin", "super_admin"));
            options.AddPolicy(SuperAdminPolicy, policy => policy.RequireRole("super_admin"));
            options.AddPolicy(ThirdPartyPolicy, policy => policy.RequireRole("third_party", "admin", "super_admin"));
        });

        services.AddScoped<IAuthorizationHandler, ResourceAuthorizationHandler>();

        return services;
    }
}

public record ResourceOwnerRequirement : IAuthorizationRequirement;
