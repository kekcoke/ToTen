using MassTransit;
using ToTen.Worker.Consumers;
using ToTen.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.ConfigureOpenTelemetry();

// Configuration
builder.Services.Configure<NotificationOptions>(
    builder.Configuration.GetSection(NotificationOptions.SectionName));

// Infrastructure Services
builder.Services.AddSingleton<INotifier, MockNotifier>();

// Add MassTransit
builder.Services.AddMassTransit(x =>
{
    // Register all consumers in the assembly
    x.AddConsumers(typeof(ItemEventsConsumer).Assembly);

    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("servicebus"));

        // Use the same topology convention as the API
        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
host.Run();
