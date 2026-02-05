using ToTen.Worker;
using ToTen.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.ConfigureOpenTelemetry();

// Add Azure Service Bus
builder.AddAzureServiceBusClient("servicebus");

// Register message processor service
builder.Services.AddSingleton<ItemEventProcessor>();

// Register the background service
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
