using Aspire.Hosting.ApplicationModel;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddAzurePostgresFlexibleServer("postgres")
                    .RunAsContainer(postgres =>
                    {
                        postgres.WithImage("postgis/postgis")
                                .WithImageTag("17-3.4");
                        postgres.WithDataVolume();
                        postgres.WithPgAdmin(pgAdmin =>
                        {
                            pgAdmin.WithHostPort(5050);
                        });
                    });

var ToTenDb = postgres.AddDatabase("ToTenDB", "ToTen");

// Add Azure Service Bus with emulator support for local development
var serviceBus = builder.AddAzureServiceBus("servicebus")
                        .RunAsEmulator(emulator =>
                        {
                            emulator.WithLifetime(ContainerLifetime.Persistent);
                        });

var queue = serviceBus.AddServiceBusQueue("items-events");
var apiQueue = serviceBus.AddServiceBusQueue("ToTen-Api-Queue");
var workerQueue = serviceBus.AddServiceBusQueue("ToTen-Worker-Queue");
var apiErrorQueue = serviceBus.AddServiceBusQueue("ToTen-Api-Error");
var workerErrorQueue = serviceBus.AddServiceBusQueue("ToTen-Worker-Error");

// Add Azure Storage with Azurite emulator for local development
var storage = builder.AddAzureStorage("storage")
                    .RunAsEmulator(emulator =>
                    {
                        emulator.WithLifetime(ContainerLifetime.Persistent);
                    });

var blobs = storage.AddBlobs("blobs");

var keycloakPassword = builder.AddParameter("KeycloakPassword", secret: true, value: "admin");
// Must match the ToTen-web-bff client's baked-in secret in ToTen-realm.json — the realm is
// baked into the Keycloak image at build time (see comment below), not live-imported, so this
// dev value and the realm JSON's literal secret have to be kept in sync by hand.
var webBffClientSecret = builder.AddParameter(
    "ToTenWebBffClientSecret", secret: true, value: "dev-web-bff-secret-change-me-9f8e7d6c5b4a");
int? keycloakPort = builder.ExecutionContext.IsRunMode ? 8080 : null;
var keycloak = builder.AddKeycloak("keycloak", adminPassword: keycloakPassword, port: keycloakPort)
                      .WithLifetime(ContainerLifetime.Persistent)
                      .WithArgs("--verbose");
// Realm is baked into the image — no WithRealmImport volume mount needed.
// To pick up realm changes: docker rm the persistent container, then dotnet run again.
if (builder.ExecutionContext.IsRunMode)
{
    keycloak = keycloak.WithDockerfile("../..", "docker/keycloak/Dockerfile.dev");
}

// AddKeycloak auto-registers a health check against the HTTPS management port (9000).
// That port uses a self-signed cert that is untrusted by the AppHost's HttpClient,
// so WaitFor(keycloak) stalls indefinitely. Replace it with an HTTP check on the
// app port (8080): /realms/master returns 200 only once Keycloak is fully started.
foreach (var hc in keycloak.Resource.Annotations.OfType<HealthCheckAnnotation>().ToList())
{
    keycloak.Resource.Annotations.Remove(hc);
}
keycloak.WithHttpHealthCheck("/realms/master");

var keycloakAuthority = ReferenceExpression.Create(
    $"{keycloak.GetEndpoint("http").Property(EndpointProperty.Url)}/realms/ToTen"
);

// Key Vault emulator (local only) — production Key Vault is provisioned by Terraform.
// Uses ghcr.io/james-gould/azure-keyvault-emulator; trust the self-signed cert in dev via
// ASPNETCORE_Kestrel__Certificates__Default__* or set AZURE_KEYVAULT_DISABLE_CHALLENGE_RESOURCE_VERIFICATION=true.
// Disabled by default: the ghcr.io image is currently not pullable (package removed/private
// upstream, confirmed via 403 DENIED on an anonymous token request — not a local auth issue),
// and no code path in Api/Worker reads KeyVault__Uri today, so nothing is lost by skipping it.
// Flip to true once a working image is available and something actually consumes the secret.
const bool EnableKeyVaultEmulator = false;
IResourceBuilder<ContainerResource>? keyVaultEmulator = null;
if (builder.ExecutionContext.IsRunMode && EnableKeyVaultEmulator)
{
    keyVaultEmulator = builder.AddContainer("keyvault-emulator", "ghcr.io/james-gould/azure-keyvault-emulator")
        .WithImageTag("latest")
        .WithLifetime(ContainerLifetime.Persistent)
        .WithHttpsEndpoint(port: 4997, targetPort: 4997, name: "vault");
}

#pragma warning disable ASPIREPROBES001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
var api = builder.AddProject<ToTen_Api>("ToTen-api")
            .WithReference(ToTenDb)
            .WithReference(serviceBus)
            .WithReference(blobs)
            .WaitFor(ToTenDb)
            .WaitFor(serviceBus)
            .WaitFor(blobs)
            .WithEnvironment("Auth__Authority", keycloakAuthority)
            .WithEnvironment("SWAGGERUI_CLIENTID", builder.Configuration["SwaggerUI:ClientId"])
            .WithEnvironment("Auth__WebBff__ClientId", "ToTen-web-bff")
            .WithEnvironment("Auth__WebBff__ClientSecret", webBffClientSecret)
            // Matches the ToTen-web-bff client's redirectUris entry in ToTen-realm.json.
            .WithEnvironment("Auth__WebBff__RedirectUri", "http://localhost:5082/auth/callback")
            .WaitFor(keycloak)
            .WithUrls(context =>
            {
                context.Urls.Add(new()
                {
                    Url = "/swagger",
                    DisplayText = "API Docs",
                    Endpoint = context.GetEndpoint("http")
                });
            })
            .WithExternalHttpEndpoints()
            .WithHttpEndpoint(name: "health", targetPort: 8081, isProxied: false) // after WithExternalHttpEndpoints because it's internal only
            .WithHttpProbe(ProbeType.Liveness, "/health/alive", periodSeconds: 10, endpointName: "health")
            .WithHttpProbe(ProbeType.Readiness, "/health/ready", periodSeconds: 10, endpointName: "health")
            .WithUrlForEndpoint("health", c => c.DisplayLocation = UrlDisplayLocation.DetailsOnly);
#pragma warning restore ASPIREPROBES001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

// Add Worker Service
var worker = builder.AddProject<ToTen_Worker>("ToTen-worker")
                    .WithReference(ToTenDb)
                    .WithReference(serviceBus)
                    .WithReference(blobs)
                    .WaitFor(ToTenDb)
                    .WaitFor(serviceBus)
                    .WaitFor(blobs);

if (keyVaultEmulator is not null)
{
    var kvUrl = keyVaultEmulator.GetEndpoint("vault").Property(EndpointProperty.Url);
    api.WithEnvironment("KeyVault__Uri", kvUrl);
    worker.WithEnvironment("KeyVault__Uri", kvUrl);
}

builder.AddAzureContainerAppEnvironment("cae");

builder.Build().Run();