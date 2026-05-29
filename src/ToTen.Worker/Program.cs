using Rebus.Config;
using ToTen.Worker.Consumers;
using ToTen.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.ConfigureOpenTelemetry();

// Configuration
builder.Services.Configure<NotificationOptions>(
    builder.Configuration.GetSection(NotificationOptions.SectionName));

// Infrastructure Services
builder.Services.AddSingleton<INotifier, MockNotifier>();

// Add Rebus
builder.Services.AddRebus(
    configure => configure
        .Transport(t => t.UseAzureServiceBus(
            builder.Configuration.GetConnectionString("servicebus"),
            "ToTen-Worker-Queue"))
);

// Register message handlers
builder.Services.AddRebusHandler<ItemEventsHandler>();
builder.Services.AddRebusHandler<NotificationHandler>();
builder.Services.AddRebusHandler<ManifestCreatedHandler>();

var host = builder.Build();
host.Run();
