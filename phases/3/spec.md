## Phase 3: Infrastructure as Code (Terraform) Migration
**Assigned Agent**: Architect Agent / DevSecOps Agent
**Objective**: Replace `azd` Bicep templates with enterprise-grade Terraform modules.

---

### Original To-Dos
- [x] Create `/terraform` directory structure (`main.tf`, `variables.tf`, `outputs.tf`, `locals.tf`).
- [x] Write Azure Container Apps Environment module (`modules/container-apps/`); includes user-assigned managed identity shared by all ACA workloads.
- [x] Write Azure Database for PostgreSQL Flexible Server module (`modules/postgres/`); PostGIS enabled via `azure.extensions` server configuration; `ToTen` and `keycloak` databases provisioned; CIDR firewall rules + Azure-services rule.
- [x] Write Azure Service Bus namespace and Keycloak container modules (`modules/service-bus/`, `modules/keycloak/`); 3 queues (`items-events`, `ToTen-Api-Queue`, `ToTen-Worker-Queue`) with `SendListen` SAS rule; Keycloak deployed as ACA container app from custom ACR image.
- [x] Write Azure SignalR Service Terraform module and configure API keys for external providers (`modules/signalr/`); Default service mode; CORS scoped to ACA environment default domain; `Microsoft.Azure.SignalR` NuGet added to `ToTen.Api`; `AddAzureSignalR()` wired with self-hosted fallback when `SignalR:ConnectionString` is empty.
- [x] Configure OpenTelemetry (OTLP) infrastructure endpoints (`modules/observability/`); workspace-based Application Insights; Log Analytics workspace; OTLP ingestion via `ApplicationInsights:ConnectionString` environment variable injected into ACA container apps.

---

### Additional Implementation Items (not in original spec)
- [x] Write Azure Storage Account module (`modules/storage/`); Standard LRS, private `blobs` container for QR codes and assets.
- [x] Write Azure Container Registry module (`modules/registry/`); Standard SKU; admin enabled; `AcrPull` role assigned to ACA managed identity; required before Phase 4 Docker image push.
- [x] Write Azure Key Vault module (`modules/key-vault/`); RBAC authorization model; Terraform SP granted `Key Vault Secrets Officer`; ACA managed identity granted `Key Vault Secrets User`; 6 secrets stored (postgres, keycloak, servicebus, acr, signalr, storage connection strings).
- [x] Wire root `main.tf` with all 9 modules in explicit dependency order; `azurerm ~> 4.0` provider; remote backend targeting dedicated `toten-tfstate-rg` / `totentfstate` storage account.
- [x] Create `envs/prod.tfvars` for single-environment apply; parameterized SKUs for multi-env extension (see `docs/adal/phase-3-multi-env-notes.md`).
- [x] Create `docker/keycloak/Dockerfile`; extends `quay.io/keycloak/keycloak:latest`; bakes `ToTen-realm.json` into `/opt/keycloak/data/import/`; runs `kc.sh build --db=postgres` at image build time; CMD `start --import-realm --optimized`.
- [x] Add Key Vault emulator (`ghcr.io/james-gould/azure-keyvault-emulator`) to `AppHost.cs` (local-only, persistent, HTTPS 4997); `KeyVault__Uri` injected into Api and Worker in run mode; `KeyVaultUri` parameter added to `IsPublishMode` block.
- [x] Add `KeyVault.Uri` and `SignalR.ConnectionString` config sections to `appsettings.json` (Api); `KeyVault.Uri` set to `https://localhost:4997` in `appsettings.Development.json` for both Api and Worker.
- [x] Add Terraform and tooling exclusions to `.gitignore` (state files, `*.auto.tfvars`, `.terraform/`) and `.dockerignore` (`terraform/`, `docs/`, `.adal/`, `CHANGELOG/`).
- [x] Write companion reference docs: `phase-3-multi-env-notes.md` (tfvars-per-env strategy) and `phase-3-vnet-private-endpoints.md` (full VNet + private endpoint design for future hardening).
- [x] Remove `IsPublishMode` block from `AppHost.cs`; Terraform owns all cloud resource provisioning. Prerequisite: Terraform modules validated against a real Azure subscription (`terraform validate` + `terraform plan` passing).
- [ ] Run `terraform init` against the bootstrap state backend and `terraform validate`; resolve any provider-level errors before Phase 4 pipeline integration.
