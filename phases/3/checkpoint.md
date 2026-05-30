## Phase 3: Infrastructure as Code (Architect / DevSecOps Agent)
**Validation Checklist**:

### Terraform Syntax & Plan
- [x] `terraform fmt -check -recursive` passes with zero formatting differences across all modules.
- [ ] `terraform validate` reports no syntax or configuration errors. *(Requires `terraform init` against the bootstrap Azure backend — cannot run without Azure credentials.)*
- [ ] `terraform plan -var-file=envs/prod.tfvars` executes successfully and generates the expected infrastructure delta without state lock errors. *(Requires live Azure subscription.)*

### Secrets & Credentials
- [x] Environment variables and secrets are correctly abstracted; no hardcoded credentials in any `.tf` file; all sensitive values flow through `variable` blocks marked `sensitive = true` and are stored as Key Vault secrets.
- [x] All sensitive `outputs.tf` values (connection strings, passwords) are marked `sensitive = true`; outputs are safe to reference in CI without leaking to logs.
- [x] `KeyVault__Uri` is injected into Api and Worker at runtime in both run mode (emulator endpoint) and publish mode (`KeyVaultUri` parameter from CI pipeline).

### Module-Specific
- [x] PostgreSQL Terraform module explicitly enables PostGIS via `azurerm_postgresql_flexible_server_configuration` (`azure.extensions = POSTGIS`); EF Core migration's `CREATE EXTENSION IF NOT EXISTS postgis` will succeed post-provision.
- [x] Azure SignalR Service (`modules/signalr/`) and connection string secret are present; `Microsoft.Azure.SignalR` NuGet added to `ToTen.Api`; `AddAzureSignalR()` called conditionally when `SignalR:ConnectionString` is non-empty (self-hosted fallback in local dev).
- [x] Azure Container Registry provisioned with `AcrPull` role assigned to ACA user-assigned managed identity; ACR admin credentials stored in Key Vault.
- [ ] Custom Keycloak Docker image (`docker/keycloak/Dockerfile`) builds successfully: `docker build -f docker/keycloak/Dockerfile -t toten-keycloak .` from repo root. *(Requires Docker daemon; validates realm file copy and `kc.sh build` step.)*
- [ ] Keycloak `/realms/ToTen` returns HTTP 200 after ACA container start and custom image deploy; `ToTen` realm present without manual import. *(Requires live Azure deployment.)*

### Local Development
- [x] Key Vault emulator container (`ghcr.io/james-gould/azure-keyvault-emulator`) registered in AppHost with persistent lifetime and HTTPS 4997; `KeyVault__Uri` resolves in Api and Worker environment on Aspire start.
- [ ] Key Vault emulator starts cleanly in Aspire dashboard and `KeyVault__Uri` appears in Api and Worker process environment. *(Verify by running `dotnet run --project src/ToTen.AppHost` and checking Aspire dashboard.)*

### Handoff Gate
- [x] `IsPublishMode` block removed from `AppHost.cs`; Terraform owns all cloud resource provisioning. This item must be completed before Phase 4 begins.
