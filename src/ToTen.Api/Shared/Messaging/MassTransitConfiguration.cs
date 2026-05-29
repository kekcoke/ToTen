using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToTen.Contracts;

namespace ToTen.Api.Shared.Messaging;

public static class MassTransitConfiguration
{
    public static IServiceCollection AddToTenMassTransit(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMassTransit(x =>
        {
            // Automatically discover consumers in the entry assembly
            x.AddConsumers(typeof(MassTransitConfiguration).Assembly);

            x.UsingAzureServiceBus((context, cfg) =>
            {
                cfg.Host(configuration.GetConnectionString("ServiceBus"));

                // Topology Configuration for standalone records
                cfg.Message<ItemMovedEvent>(m => m.SetEntityName("item-moved-topic"));
                
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
