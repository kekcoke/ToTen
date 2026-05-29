# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Run the full application locally (starts all containers via Aspire)
dotnet run --project src/ToTen.AppHost
# or
aspire run

# Build the solution
dotnet build

# Run all integration tests
dotnet test

# Run tests for a specific project
dotnet test tests/ToTen.Api.IntegrationTests

# Add an EF Core migration (run from repo root)
dotnet ef migrations add <MigrationName> --project src/ToTen.Api --startup-project src/ToTen.Api

# Apply pending migrations
dotnet ef database update --project src/ToTen.Api --startup-project src/ToTen.Api

# Deploy to Azure
aspire deploy
```

## Architecture

This is a **.NET 10 Vertical Slice API** with an Aspire orchestration layer, a background worker, and shared contracts. The solution has five projects:

| Project | Role |
|---|---|
| `ToTen.AppHost` | Aspire orchestrator — wires up all infrastructure (Postgres, Service Bus, Azurite, Keycloak) and launches the other services |
| `ToTen.Api` | ASP.NET Core Minimal API — all business logic, organized by vertical slice |
| `ToTen.Worker` | .NET Worker Service — consumes Rebus messages from Azure Service Bus |
| `ToTen.Contracts` | Shared library — message event records (`ItemEvents.cs`) shared between Api and Worker |
| `ToTen.ServiceDefaults` | Shared Aspire service defaults (health checks, OTEL, resilience) applied to both Api and Worker |

### Vertical Slice Organization (ToTen.Api)

Each domain feature lives under `Features/<Domain>/` with one subfolder per operation:

```
Features/Items/
├── ItemsEndpoints.cs       # groups and maps all routes for this feature
├── Constants/
├── CreateItem/
│   ├── CreateItemEndpoint.cs   # all business logic inline (EF + publish event)
│   └── CreateItemDtos.cs
└── GetItems/...
```

Current feature domains: `Categories`, `Communications`, `Items`, `Manifests`, `Marketplace`, `Memberships`, `Organizations`, `Storage`, `Users`.

Cross-cutting concerns live under `Shared/`:
- `Authentication/` — JWT Bearer config
- `Authorization/` — role policies and resource authorization
- `Identity/` — `IIdentityManager` abstraction + `KeycloakIdentityManager` (decouples core API from Keycloak-specific libraries)
- `Messaging/` — Rebus configuration (`RebusConfiguration.cs`), `IEventPublisher`/`EventPublisher`, Service Bus helpers
- `Infrastructure/` — `AzureStorageService`/`IStorageService` for Blob Storage (QR codes)
- `OpenApi/`, `Cors/`, `ErrorHandling/`

### Messaging (Rebus)

The Api publishes item lifecycle events; the Worker consumes them. Both use **Rebus** (`Rebus.AzureServiceBus`) over Azure Service Bus. Three queues are provisioned by AppHost:
- `items-events` — item lifecycle
- `ToTen-Api-Queue` — Rebus input for the API
- `ToTen-Worker-Queue` — Rebus input for the Worker

In local development the Service Bus runs as an emulator container (persistent lifetime). Contracts are defined in `ToTen.Contracts/Events/ItemEvents.cs`. Worker handlers are in `ToTen.Worker/Consumers/` and implement `IHandleMessages<T>`.

### Local Infrastructure (Aspire)

`AppHost.cs` provisions all dependencies as containers:
- **PostgreSQL** + pgAdmin (port 5050)
- **Azure Service Bus emulator** (persistent)
- **Azurite** (Azure Storage emulator, persistent) — blob container `"blobs"`
- **Keycloak** (port 8080, persistent, realm imported from `src/ToTen.AppHost/realms/`)

The Api exposes a separate health endpoint on port 8081 (`/health/alive`, `/health/ready`).

### Authentication

Keycloak handles JWT auth. The realm is `ToTen`. Local credentials: `demo`/`demo` (user), `admin`/`admin` (admin). After Aspire starts, the Swagger UI (`/swagger`) is accessible via the Aspire Dashboard "API Docs" link — use the Authorize button for OAuth2 flow.

### Integration Tests

Tests use `WebApplicationFactory` with `Testcontainers.PostgreSql`, `NSubstitute`, `Shouldly`, and `AutoFixture`. Authentication is bypassed via a `TestAuthHandler`. `MockEventPublisher` replaces `IEventPublisher` in the test host.
