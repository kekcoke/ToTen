using Azure.Identity;
using Rebus.Config;
using Rebus.Retry.Simple;
using ToTen.Worker.Consumers;
using ToTen.Worker.Data;
using ToTen.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.ConfigureOpenTelemetry();

var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    ManagedIdentityClientId = builder.Configuration["AZURE_CLIENT_ID"]
});

builder.AddWorkerNpgsql<WorkerDbContext>("ToTenDB", credential);

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
        .Options(o => o.RetryStrategy(errorQueueName: "ToTen-Worker-Error", maxDeliveryAttempts: 5))
);

// Register message handlers
builder.Services.AddRebusHandler<ItemEventsHandler>();
builder.Services.AddRebusHandler<NotificationHandler>();
builder.Services.AddRebusHandler<ManifestCreatedHandler>();

var host = builder.Build();
await host.MigrateWorkerDbAsync();
host.Run();
