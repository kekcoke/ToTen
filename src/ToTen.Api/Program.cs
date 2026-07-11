using Azure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using ToTen.Api.Data;
using ToTen.Api.Features.Auth;
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
using ToTen.Api.Shared.RateLimiting;

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
builder.Services.AddToTenIdentityAndSignalR(builder.Configuration);
builder.Services.AddToTenRebus(builder.Configuration);

// Configure authentication options with validation
builder.Services.AddOptions<AuthOptions>()
                .Bind(builder.Configuration.GetSection(AuthOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

// Not ValidateOnStart: unset until the web BFF's real deployed redirect URI is known
// (see terraform/variables.tf's keycloak_web_bff_redirect_uri) — mobile's bearer path
// must keep working even before the web BFF is fully configured. Validated lazily on
// first use by whichever Features/Auth endpoint actually needs it.
builder.Services.AddOptions<WebBffOptions>()
                .Bind(builder.Configuration.GetSection(WebBffOptions.SectionName))
                .ValidateDataAnnotations();

builder.Services.AddOptions<StorageOptions>()
                .Bind(builder.Configuration.GetSection(StorageOptions.SectionName));

// Register the JWT Bearer options configurator first
builder.Services.ConfigureOptions<JwtBearerOptionsSetup>();

// Default "smart" policy scheme forwards to Bearer (mobile — Authorization header present)
// or Cookies (web BFF — no header, relies on the session cookie). Existing endpoints'
// RequireAuthorization(...)/[Authorize(Policy = ...)] calls need no change: they evaluate
// against whichever principal the policy scheme forwarded to.
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "smart";
    options.DefaultChallengeScheme = "smart";
})
.AddPolicyScheme("smart", "Bearer or Cookie", options =>
{
    options.ForwardDefaultSelector = context =>
        context.Request.Headers.ContainsKey("Authorization")
            ? JwtBearerDefaults.AuthenticationScheme
            : CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddJwtBearer()
.AddCookie(options =>
{
    options.Cookie.Name = "__Host-ToTen-Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax; // Strict would break the top-level redirect back from Keycloak's callback
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // mirrors the realm's ssoSessionIdleTimeout (1800s)
    options.SlidingExpiration = true;
    options.Events.OnValidatePrincipal = CookieTokenRefreshEvents.RefreshIfNeededAsync;
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; }; // JSON API — no HTML login page to redirect to
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});

builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-Token");

// Flattens Keycloak's nested realm_access/resource_access role claims and
// raw sub/email claims into the ClaimTypes.* shape KeycloakIdentityManager reads
builder.Services.AddTransient<IClaimsTransformation, KeycloakClaimsTransformation>();

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

builder.Services.AddToTenRateLimiting();

// Azure Blob Storage client
builder.AddAzureBlobServiceClient("blobs");

// Infrastructure Services
builder.Services.AddScoped<IStorageService, AzureStorageService>();
builder.Services.AddScoped<IQRCodeService, QRCodeService>();

var app = builder.Build();

app.UseCors();

app.UseRateLimiter();

app.UseAntiforgery();

// CsrfValidationFilter applies globally to every endpoint mapped through this group (not
// per-endpoint) so existing Features/*/*.cs registrations need no individual changes. It only
// acts on mutating requests authenticated via the Cookies scheme, so mobile's bearer callers
// and every GET endpoint are unaffected. SignalR hubs are mapped outside this group — they
// use their own query-string bearer token pattern, unrelated to cookie CSRF.
app.MapDefaultEndpoints();

var routes = app.MapGroup(string.Empty).AddEndpointFilter<CsrfValidationFilter>();

routes.MapInventoryItems();
routes.MapCategories();
routes.MapStorageEndpoints();
routes.MapManifestEndpoints();
routes.MapMarketplaceEndpoints();
routes.MapOrganizationEndpoints();
routes.MapMembershipEndpoints();
routes.MapUserEndpoints();
routes.MapAuthEndpoints();
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