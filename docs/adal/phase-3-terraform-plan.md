# Phase 3 Implementation Plan: Infrastructure as Code (Terraform Migration)
**Date**: 2026-05-29
**Status**: Planning
**Agents**: Architect Agent + DevSecOps Agent
**Branch**: `implement/phase-3`

---

## Context & Baseline

Phase 1 expanded the domain model (15-table schema, PostGIS, JSONB, 6-tier roles). Phase 2 implemented vertical slice business logic (Storage, Manifest, Marketplace, Communications, Organizations, Authorization). The current deployment pipeline uses `azd provision` + `azd deploy`, which auto-generates Bicep from the Aspire `IsPublishMode` block in `AppHost.cs`.

**Phase 3 objective**: Replace the `azd`-generated Bicep pipeline with explicit, enterprise-grade Terraform modules that give full control over resource configuration and enable the DevSecOps pipeline in Phase 4.

---

## Resolved Design Decisions

| Gap | Decision |
|---|---|
| SignalR mode | `Default` — matches the existing `ChatHub` WebSocket pattern; supports gradual migration from self-hosted |
| Terraform state backend | Dedicated Azure Storage Account (`toten-prod-tfstate`) separate from the application storage account |
| Environment strategy | Single `prod` target for this implementation; see `phase-3-multi-env-notes.md` for dev/staging/prod extension guide |
| Key Vault | Provision `azurerm_key_vault` in Terraform; use `azure-keyvault-emulator` (james-gould) for local dev via AppHost; `KeyVaultUri` parameter injected in publish mode |
| Network isolation | Public endpoints with firewall rules for this implementation; see `phase-3-vnet-private-endpoints.md` for full VNet + private endpoint design |
| Keycloak realm import | Bake `ToTen-realm.json` into a custom Docker image (`docker/keycloak/Dockerfile`) pushed to ACR; no runtime import complexity |

---

## Current Azure Resources Inferred from AppHost.cs

| Resource | Aspire API | Notes |
|---|---|---|
| Azure Container Apps Environment | `AddAzureContainerAppEnvironment("cae")` | Hosts API + Worker containers |
| Azure PostgreSQL Flexible Server | `AddAzurePostgresFlexibleServer("postgres")` | Needs PostGIS extension enabled |
| Azure Service Bus Namespace | `AddAzureServiceBus("servicebus")` | 3 queues: `items-events`, `ToTen-Api-Queue`, `ToTen-Worker-Queue` |
| Azure Storage Account | `AddAzureStorage("storage")` | Blob container `blobs` (QR codes, assets) |
| Azure Application Insights | `AddAzureApplicationInsights("app-insights")` | OTLP sink (publish mode only) |
| Keycloak (container) | `AddKeycloak(...)` | Runs in ACA; custom Docker image baked with realm JSON |
| Azure SignalR Service | _(not yet provisioned)_ | Default mode; Terraform module + `AddAzureSignalR()` wiring |
| Azure Container Registry | _(not yet provisioned)_ | Required by Phase 4; provisioned here |
| Azure Key Vault | _(not yet provisioned)_ | Secrets store; emulated locally via `azure-keyvault-emulator` |

---

## Proposed `/terraform` Directory Structure

```
/terraform/
├── main.tf                        # Provider config, backend, module wiring
├── variables.tf                   # All input variables (env, location, SKUs, secrets)
├── outputs.tf                     # Exported resource FQDNs, IDs, connection strings (Key Vault refs)
├── locals.tf                      # Naming convention: toten-{env}-{resource-type}
├── modules/
│   ├── container-apps/            # ACA Environment + Log Analytics workspace
│   │   ├── main.tf
│   │   ├── variables.tf
│   │   └── outputs.tf
│   ├── postgres/                  # Flexible Server + PostGIS extension + firewall rules
│   │   ├── main.tf
│   │   ├── variables.tf
│   │   └── outputs.tf
│   ├── service-bus/               # Standard namespace + 3 queues + SAS policies
│   │   ├── main.tf
│   │   ├── variables.tf
│   │   └── outputs.tf
│   ├── storage/                   # App storage account + blobs container + lifecycle policy
│   │   ├── main.tf
│   │   ├── variables.tf
│   │   └── outputs.tf
│   ├── key-vault/                 # Key Vault + secrets + ACA managed identity access policy
│   │   ├── main.tf
│   │   ├── variables.tf
│   │   └── outputs.tf
│   ├── keycloak/                  # Keycloak as ACA container app (custom image from ACR, PostgreSQL-backed)
│   │   ├── main.tf
│   │   ├── variables.tf
│   │   └── outputs.tf
│   ├── signalr/                   # Azure SignalR Service (Default mode)
│   │   ├── main.tf
│   │   ├── variables.tf
│   │   └── outputs.tf
│   ├── registry/                  # Azure Container Registry (Standard SKU)
│   │   ├── main.tf
│   │   ├── variables.tf
│   │   └── outputs.tf
│   └── observability/             # Application Insights (workspace-based) + Log Analytics + OTLP config
│       ├── main.tf
│       ├── variables.tf
│       └── outputs.tf

/docker/
└── keycloak/
    └── Dockerfile                 # Custom image: quay.io/keycloak/keycloak + baked ToTen-realm.json
```

---

## Module Specifications

### 1. `container-apps` Module
- Provision `azurerm_log_analytics_workspace` (required by ACA environment).
- Provision `azurerm_container_app_environment`.
- Output: environment ID, Log Analytics workspace ID and key.
- **Checkpoint**: `terraform plan` must show environment creation with no pending changes.

### 2. `postgres` Module
- Provision `azurerm_postgresql_flexible_server` (PostgreSQL 17).
- Configure `azurerm_postgresql_flexible_server_configuration` for `azure.extensions` = `POSTGIS`.
- Provision firewall rule allowing ACA subnet (requires VNet integration decision — see Gap #5).
- Create databases: `ToTen`, `keycloak`.
- Output: FQDN, admin username reference (Key Vault secret, not plaintext).
- **Checkpoint**: PostGIS extension must be activatable post-provision via `CREATE EXTENSION IF NOT EXISTS postgis`.

### 3. `service-bus` Module
- Provision `azurerm_servicebus_namespace` (Standard tier minimum; Premium for VNet isolation).
- Provision `azurerm_servicebus_queue` × 3: `items-events`, `ToTen-Api-Queue`, `ToTen-Worker-Queue`.
- Provision SAS authorization rules (`Send`, `Listen`) per queue.
- Output: namespace connection string reference.
- **Checkpoint**: Queue entities visible in Azure Portal after `terraform apply`.

### 4. `storage` Module
- Provision `azurerm_storage_account` (LRS replication, Standard tier).
- Provision `azurerm_storage_container` named `blobs`.
- Output: blob endpoint, account key reference.

### 5. `key-vault` Module
- Provision `azurerm_key_vault` with RBAC authorization model (`enable_rbac_authorization = true`).
- Assign `Key Vault Secrets Officer` role to the Terraform service principal (for secret writes during `apply`).
- Assign `Key Vault Secrets User` role to the ACA managed identity (for runtime secret reads).
- Store secrets: `postgres-admin-password`, `keycloak-admin-password`, `servicebus-connection-string`, `acr-admin-password`, `signalr-connection-string`.
- **Local dev**: `azure-keyvault-emulator` container in AppHost (persistent lifetime, HTTPS port 4997); `KeyVault__Uri` env var set to emulator endpoint. See AppHost.cs and `appsettings.Development.json` updates.
- Output: Key Vault URI (for injection into ACA environment variables and AppHost `KeyVaultUri` parameter).
- **Checkpoint**: `az keyvault secret list --vault-name <name>` returns all expected secrets post-apply.

### 6. `keycloak` Module
- Provision `azurerm_container_app` for Keycloak using custom ACR image (see `docker/keycloak/Dockerfile`).
- **Custom image**: Extends `quay.io/keycloak/keycloak:latest`, copies `ToTen-realm.json` to `/opt/keycloak/data/import/`, sets `--import-realm` start argument. Built and pushed to ACR by Phase 4 CI.
- Environment vars: `KC_DB=postgres`, `KC_DB_URL`, `KC_DB_USERNAME`, `KC_DB_PASSWORD` (Key Vault secret ref), `KC_HTTP_ENABLED=true`, `KC_PROXY_HEADERS=xforwarded`, `KC_HOSTNAME_STRICT=false`.
- Output: Keycloak FQDN for `Auth__Authority` env var injection into API container app.
- **Checkpoint**: `/realms/ToTen` returns HTTP 200 after deploy; realm import verified without manual intervention.

### 7. `signalr` Module
- Provision `azurerm_signalr_service` (Default service mode, Standard_S1 SKU).
- Default mode supports the existing `ChatHub` WebSocket pattern used in `IdentityAndSignalRConfiguration.cs`.
- CORS origins configured to the ACA API FQDN.
- Connection string stored in Key Vault (`signalr-connection-string`).
- Output: connection string (Key Vault reference).
- **Code touch required**: Add `Microsoft.Azure.SignalR` NuGet to `ToTen.Api`, update `AddSignalR()` to `AddSignalR().AddAzureSignalR(connectionString)` in `IdentityAndSignalRConfiguration.cs`. This is an infrastructure-adjacent change with no business logic impact.

### 8. `registry` Module
- Provision `azurerm_container_registry` (Standard SKU).
- Assign `AcrPull` role to the ACA managed identity.
- Admin credentials stored in Key Vault (`acr-admin-password`).
- Output: login server, admin username (for Phase 4 CI Docker push step).
- **Rationale**: Phase 4 CI pushes API, Worker, and Keycloak Docker images to ACR; this module must be applied before Phase 4 begins.

### 9. `observability` Module
- Provision `azurerm_application_insights` (workspace-based, referencing Log Analytics from `container-apps` module).
- Configure OTLP ingestion endpoint (Application Insights natively exposes an OTLP endpoint at `https://<region>.in.applicationinsights.azure.com/v2/track`).
- Output: instrumentation key, connection string, OTLP endpoint URL.
- **Note**: `ToTen.ServiceDefaults` already wires OTEL via `AddServiceDefaults()`. The connection string will be injected into the ACA container app environment variables.

---

## Terraform Root Configuration

### `main.tf` responsibilities
- `terraform` block: required providers (`azurerm ~> 4.0`), backend block (see Gap #2).
- `provider "azurerm"` with `features {}`.
- Module calls wired with cross-module outputs (e.g., `module.container_apps.environment_id` passed into `module.keycloak`).

### `variables.tf` top-level inputs
| Variable | Type | Description |
|---|---|---|
| `environment` | `string` | `dev`, `staging`, `prod` |
| `location` | `string` | Azure region (e.g., `eastus`) |
| `postgres_admin_password` | `string` (sensitive) | PostgreSQL admin password |
| `keycloak_admin_password` | `string` (sensitive) | Keycloak admin password |
| `allowed_cidr_ranges` | `list(string)` | For Postgres firewall rules |

### `locals.tf` naming convention
All resource names follow: `toten-{env}-{resource-type}` (e.g., `toten-prod-postgres`, `toten-prod-cae`).

### `outputs.tf` — consumed by Phase 4 CI
- Container Registry login server
- ACA environment ID
- Application Insights connection string
- Keycloak FQDN

---

## `IsPublishMode` Block in AppHost.cs — Disposition

Once Terraform provisions all cloud resources, the `IsPublishMode` block in `AppHost.cs` should be **removed**. The Aspire publish mode currently:
- Adds PostgreSQL password auth parameters
- Creates the `keycloakDB` database reference
- Wires Keycloak environment variables
- Adds Application Insights

All of these become Terraform's responsibility. The Aspire `AddAzureContainerAppEnvironment("cae")` call can remain (it generates the ACA deployment manifest), but the `IsPublishMode` configuration logic moves to Terraform variables and container app environment injection.

---

## Implementation Sequence

1. **Create `/terraform` directory structure** — scaffold all module directories with empty `main.tf`, `variables.tf`, `outputs.tf` files.
2. **Write `observability` module** — Log Analytics is a dependency root for ACA environment and Application Insights.
3. **Write `container-apps` module** — ACA environment depends on Log Analytics workspace ID.
4. **Write `postgres` module** — databases (`ToTen`, `keycloak`) required by both API and Keycloak.
5. **Write `service-bus` module** — independent; no cross-module dependencies.
6. **Write `storage` module** — independent; separate from the `tfstate` storage account.
7. **Write `registry` module** — independent; must exist before the Keycloak image can be pushed.
8. **Write `key-vault` module** — depends on ACA managed identity from `container-apps`; secrets populated after other modules output their values.
9. **Write `keycloak` module** — depends on ACA environment, PostgreSQL FQDN, ACR login server, Key Vault secret refs.
10. **Write `signalr` module** — depends on ACA FQDN (for CORS origin).
11. **Wire root `main.tf`** — connect all modules, configure remote backend (dedicated `toten-prod-tfstate` storage account).
12. **Create `docker/keycloak/Dockerfile`** — extend base Keycloak image, copy `src/ToTen.AppHost/realms/ToTen-realm.json`, set `--import-realm` arg.
13. **Update `.dockerignore`** — add `terraform/`, `docs/`, `.adal/`, `CHANGELOG/` exclusions; confirm realm files are not excluded.
14. **Add Key Vault emulator to AppHost.cs** — local-only container (`IsRunMode`), wire `KeyVault__Uri` env var to Api and Worker; add `KeyVaultUri` parameter to `IsPublishMode` block.
15. **Update `appsettings.json` + `appsettings.Development.json`** — add `KeyVault__Uri` to Api and Worker config.
16. **Add `Microsoft.Azure.SignalR` NuGet + update `IdentityAndSignalRConfiguration.cs`** — `AddSignalR().AddAzureSignalR(connectionString)`.
17. **Remove `IsPublishMode` block from AppHost.cs** — clean handoff; Aspire's `AddAzureContainerAppEnvironment("cae")` remains for ACA deployment manifest generation.
18. **`terraform validate` + `terraform plan`** — checkpoint before Phase 4 CI integration.

---

## Checkpoint Requirements (per Architect Agent mandate)

- `terraform validate` passes with zero errors.
- `terraform plan` produces a clean diff (no unexpected destroys on re-plan).
- PostGIS extension activatable post-provision without manual intervention.
- Keycloak `/realms/ToTen` returns HTTP 200 after ACA container start; `ToTen` realm present without manual import.
- All sensitive outputs (passwords, connection strings) route through Azure Key Vault secret references — never plaintext in `outputs.tf`.
- `keyVaultEmulator` container starts cleanly in local Aspire run; `KeyVault__Uri` resolves in Api and Worker logs.
