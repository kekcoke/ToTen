using Azure.Identity;
using ToTen.Api.Data;
using ToTen.Api.Features.Items;
using ToTen.Api.Shared.Cors;
using ToTen.Api.Shared.ErrorHandling;
using ToTen.Api.Shared.OpenApi;
using ToTen.Api.Shared.Authentication;
using Microsoft.AspNetCore.HttpLogging;
using ToTen.Api.Features.Categories;
using ToTen.Api.Shared.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails()
                .AddExceptionHandler<GlobalExceptionHandler>();

var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    ManagedIdentityClientId = builder.Configuration["AZURE_CLIENT_ID"]
});

builder.AddToTenNpgsql<ToTenContext>("ToTenDB", credential);

// Add Service Bus messaging
builder.AddServiceBusMessaging("servicebus");

// Configure authentication options with validation
builder.Services.AddOptions<AuthOptions>()
                .Bind(builder.Configuration.GetSection(AuthOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

// Register the JWT Bearer options configurator first
builder.Services.ConfigureOptions<JwtBearerOptionsSetup>();

// Then add the authentication services
builder.Services.AddAuthentication()
                .AddJwtBearer();

builder.Services.AddAuthorizationBuilder();

builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod |
                            HttpLoggingFields.RequestPath |
                            HttpLoggingFields.ResponseStatusCode |
                            HttpLoggingFields.Duration;
    options.CombineLogs = true;
});

builder.AddToTenOpenApi();

builder.AddToTenCors();

builder.Services.AddValidation();

var app = builder.Build();

app.UseCors();

app.MapDefaultEndpoints();
app.MapInventoryItems();
app.MapCategories();

app.UseHttpLogging();

if (app.Environment.IsDevelopment())
{
    app.UseToTenSwaggerUI();
}
else
{
    app.UseExceptionHandler();
}

app.UseStatusCodePages();

await app.MigrateDbAsync();

app.Run();