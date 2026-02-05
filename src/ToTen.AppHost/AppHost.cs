using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddAzurePostgresFlexibleServer("postgres")
                    .RunAsContainer(postgres =>
                    {
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

var keycloakPassword = builder.AddParameter("KeycloakPassword", secret: true, value: "admin");
int? keycloakPort = builder.ExecutionContext.IsRunMode ? 8080 : null;
var keycloak = builder.AddKeycloak("keycloak", adminPassword: keycloakPassword, port: keycloakPort)
                      .WithLifetime(ContainerLifetime.Persistent);

var keycloakAuthority = ReferenceExpression.Create(
    $"{keycloak.GetEndpoint("http").Property(EndpointProperty.Url)}/realms/ToTen"
);

#pragma warning disable ASPIREPROBES001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
var api = builder.AddProject<ToTen_Api>("ToTen-api")
            .WithReference(ToTenDb)
            .WithReference(serviceBus)
            .WaitFor(ToTenDb)
            .WaitFor(serviceBus)
            .WithEnvironment("Auth__Authority", keycloakAuthority)
            .WithEnvironment("SWAGGERUI_CLIENTID", builder.Configuration["SwaggerUI:ClientId"])
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
                    .WithReference(serviceBus)
                    .WaitFor(serviceBus);

if (builder.ExecutionContext.IsRunMode)
{
    keycloak.WithDataVolume()
            .WithRealmImport("./realms");
}

if (builder.ExecutionContext.IsPublishMode)
{
    var postgresUser = builder.AddParameter("PostgresUser", value: "postgres");
    var postgresPassword = builder.AddParameter("PostgresPassword", secret: true);
    postgres.WithPasswordAuthentication(userName: postgresUser, password: postgresPassword);

    var keycloakDb = postgres.AddDatabase("keycloakDB", "keycloak");

    var keycloakDbUrl = ReferenceExpression.Create(
        $"jdbc:postgresql://{postgres.Resource.HostName}/{keycloakDb.Resource.DatabaseName}"
    );

    keycloak.WithEnvironment("KC_HTTP_ENABLED", "true")
            .WithEnvironment("KC_PROXY_HEADERS", "xforwarded")
            .WithEnvironment("KC_HOSTNAME_STRICT", "false")
            .WithEnvironment("KC_DB", "postgres")
            .WithEnvironment("KC_DB_URL", keycloakDbUrl)
            .WithEnvironment("KC_DB_USERNAME", postgresUser)
            .WithEnvironment("KC_DB_PASSWORD", postgresPassword)
            .WithEndpoint("http", e => e.IsExternal = true);

    var insights = builder.AddAzureApplicationInsights("app-insights");
    api.WithReference(insights);
    worker.WithReference(insights);
}

builder.AddAzureContainerAppEnvironment("cae");

builder.Build().Run();