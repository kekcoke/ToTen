using Azure.Identity;
using ToTen.Api.Data;
using ToTen.Api.Features.Items;
using ToTen.Api.Shared.Cors;
using ToTen.Api.Shared.ErrorHandling;
using ToTen.Api.Shared.OpenApi;
using ToTen.Api.Shared.Authentication;
using Microsoft.AspNetCore.HttpLogging;
using ToTen.Api.Features.Categories;
using ToTen.Api.Features.Manifests;
using ToTen.Api.Features.Marketplace;
using ToTen.Api.Features.Organizations;
using ToTen.Api.Features.Memberships;
using ToTen.Api.Features.Users;
using ToTen.Api.Features.Storage;
using ToTen.Api.Shared.Authorization;
using ToTen.Api.Shared.Infrastructure;
using ToTen.Api.Shared.Identity;
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

// Infrastructure (Identity, SignalR, Rebus)
builder.Services.AddToTenIdentityAndSignalR();
builder.Services.AddToTenRebus(builder.Configuration);

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

builder.Services.AddToTenAuthorization();

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

// Infrastructure Services
builder.Services.AddScoped<IStorageService, AzureStorageService>();
builder.Services.AddScoped<IQRCodeService, QRCodeService>();

var app = builder.Build();

app.UseCors();

app.MapDefaultEndpoints();
app.MapInventoryItems();
app.MapCategories();
app.MapStorageEndpoints();
app.MapManifestEndpoints();
app.MapMarketplaceEndpoints();
app.MapOrganizationEndpoints();
app.MapMembershipEndpoints();
app.MapUserEndpoints();
app.MapToTenHubs();

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