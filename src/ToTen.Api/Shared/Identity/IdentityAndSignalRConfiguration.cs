using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using ToTen.Api.Features.Communications;

namespace ToTen.Api.Shared.Identity;

public static class IdentityAndSignalRConfiguration
{
    public static IServiceCollection AddToTenIdentityAndSignalR(this IServiceCollection services)
    {
        services.AddScoped<IIdentityManager, KeycloakIdentityManager>();
        services.AddSignalR();

        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    
                    // SignalR sends tokens in query string for WebSockets
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });

        return services;
    }

    public static void MapToTenHubs(this WebApplication app)
    {
        app.MapHub<ChatHub>("/hubs/chat");
    }
}
