using Rebus.Config;
using Rebus.Retry.Simple;
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
                .Options(o => o.RetryStrategy(errorQueueName: "ToTen-Api-Error", maxDeliveryAttempts: 5))
        );

        return services;
    }
}
