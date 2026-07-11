using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToTen.Api.Features.Communications;

namespace ToTen.Api.Shared.Identity;

public static class IdentityAndSignalRConfiguration
{
    public static IServiceCollection AddToTenIdentityAndSignalR(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IIdentityManager, KeycloakIdentityManager>();

        services.AddScoped<IKeycloakTokenClient, KeycloakTokenClient>();
        services.AddHttpClient(KeycloakTokenClient.HttpClientName);

        var signalRConnectionString = configuration["SignalR:ConnectionString"];
        var signalR = services.AddSignalR();
        if (!string.IsNullOrWhiteSpace(signalRConnectionString))
            signalR.AddAzureSignalR(signalRConnectionString);

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
