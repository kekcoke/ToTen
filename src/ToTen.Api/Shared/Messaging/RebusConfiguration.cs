using Rebus.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ToTen.Api.Shared.Messaging;

public static class RebusConfiguration
{
    public static IServiceCollection AddToTenRebus(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRebus(
            configure => configure
                .Transport(t => t.UseAzureServiceBus(
                    configuration.GetConnectionString("ServiceBus"),
                    "ToTen-Api-Queue"))
        );

        return services;
    }
}
