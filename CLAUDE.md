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

# Infrastructure lifecycle (scripts/toten.sh)
./scripts/toten.sh bootstrap     # One-time: register Azure providers, create TF state storage, Entra app
./scripts/toten.sh plan          # terraform plan (reads envs/prod.tfvars + envs/secrets.tfvars)
./scripts/toten.sh apply         # terraform apply with preflight checks
./scripts/toten.sh destroy       # Tear down all Azure resources (prompts confirmation)
./scripts/toten.sh smoke-tests   # Validate live API (/openapi/v1.json) and Keycloak realm

# Docker image builds (mirrors CI)
docker build -t toten-api     -f docker/api/Dockerfile .
docker build -t toten-worker  -f docker/worker/Dockerfile .
docker build -t toten-keycloak -f docker/keycloak/Dockerfile .

# Acceptance + performance tests (require a deployed environment)
robot --outputdir test-results/robot tests/ToTen.AcceptanceTests/
jmeter -n -t tests/performance/move-item-baseline.jmx -Jbase_url=<api_fqdn>
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

### Feature Domains

Nine vertical-slice domains under `Features/`: `Categories`, `Communications` (SignalR `ChatHub`), `Items`, `Manifests`, `Marketplace`, `Memberships`, `Organizations`, `Storage`, `Users`.

### Worker Consumers

Three message consumers under `ToTen.Worker/Consumers/` implementing `IHandleMessages<T>`:
- `ItemEventsConsumer` — item lifecycle events
- `ManifestCreatedConsumer` — manifest creation events
- `NotificationConsumer` — push notifications

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

Post-deploy test suites (run against a live environment):
- **Acceptance tests** — Robot Framework, `tests/ToTen.AcceptanceTests/`
- **Performance tests** — JMeter, `tests/performance/move-item-baseline.jmx`

### Terraform Infrastructure

IaC lives in `terraform/`. State is stored in Azure Blob Storage (`totentfstate` / `tfstate-prod`). Provider: `azurerm ~> 4.0`.

| Module | Key Resources |
|---|---|
| `observability` | Log Analytics Workspace, Application Insights |
| `container-apps` | ACA Environment, user-assigned managed identity |
| `postgres` | PostgreSQL Flexible Server v17, `ToTen` + `keycloak` databases, PostGIS |
| `service-bus` | Namespace, 3 queues (`items-events`, `ToTen-Api-Queue`, `ToTen-Worker-Queue`) |
| `storage` | Storage Account (LRS), blob container `blobs` |
| `registry` | Container Registry (Standard), AcrPull role assignment |
| `signalr` | SignalR Service (Standard_S1) |
| `key-vault` | Key Vault, 6 secrets, 2 role assignments |
| `keycloak` | Container App from custom ACR image |
| `apps` | API + Worker Container Apps, env vars wired from all above modules |

Dependency order: `observability` → `container-apps` → core services → `key-vault` → `keycloak` → `apps`.

Use `./scripts/toten.sh` for all lifecycle operations rather than calling `terraform` directly — it handles preflight checks, secrets injection, and output capture.

### CI/CD Pipeline

Two GitHub Actions workflows (`.github/workflows/`):

**`azure-dev.yml`** — triggers on push/PR to `main` and `workflow_dispatch`.

| Job | Runs on | Condition |
|---|---|---|
| `lint-test` | all branches | always |
| `docker-build-push` | all branches | build always; push images to ACR on `main` only |
| `terraform` | all branches | plan always; apply on `main`; post plan comment on PR |
| `deploy` | `main` only | `az containerapp update` for API + Worker |
| `dast-scan` | `main` only | OWASP ZAP baseline against live API FQDN |
| `robot-tests` | `main` only | Robot Framework acceptance suite |
| `performance-test` | `main` only | JMeter baseline load test |
| `nuget-publish` | `main` only | Pack + push `ToTen.Contracts` to GitHub Packages |

**`codeql.yml`** — CodeQL C# static analysis; triggers on push/PR to `main` and weekly (Monday 03:00 UTC).
