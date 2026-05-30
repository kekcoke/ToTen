# Phase 4 Implementation Plan: DevSecOps & CI/CD Integration
**Date**: 2026-05-29
**Status**: Implemented
**Agent**: DevSecOps Agent
**Branch**: `main`

---

## Context & Baseline

Phase 3 replaced all `azd`-generated Bicep provisioning with nine Terraform modules (container-apps, postgres, service-bus, storage, registry, signalr, key-vault, observability, keycloak). The `IsPublishMode` block was removed from `AppHost.cs` — Terraform is the sole owner of cloud resource provisioning.

**Phase 4 objective**: Replace the `azd`-based GitHub Actions workflow with a Terraform-native, security-scanned delivery pipeline; containerize the Api and Worker services; add SAST/DAST gates; and publish `ToTen.Contracts` as an internal NuGet package.

---

## Resolved Design Decisions

| Gap | Decision |
|---|---|
| ACA deployment for Api/Worker | New `terraform/modules/apps/` module — Terraform is source of truth for all ACA container apps |
| NuGet scope | `ToTen.Contracts` only — `ToTen.ServiceDefaults` is Aspire-coupled (`IsAspireSharedProject=true`) and not suitable for general NuGet distribution |
| NuGet feed | GitHub Packages via `GITHUB_TOKEN` — zero extra infrastructure, integrated with repo |
| SAST tool | GitHub Advanced Security (CodeQL) in a dedicated `codeql.yml` workflow with weekly schedule |
| DAST scope | OWASP ZAP baseline scan post-deploy against live ACA FQDN; `fail_action: false` until clean baseline is established |
| Docker build context | Repo root for all Dockerfiles — required so `dotnet publish` can resolve `ProjectReference`s to `ToTen.Contracts` and `ToTen.ServiceDefaults` |
| ACR name | `totenprodacr` — derived from `replace("toten-prod", "-", "") + "acr"` per Phase 3 naming convention |
| Azure auth in CI | OIDC federated credentials (existing `AZURE_CLIENT_ID` / `AZURE_TENANT_ID` / `AZURE_SUBSCRIPTION_ID` vars); no new service principal secrets |

---

## Job Graph

```
push/PR to main
       │
       ├─── lint-test (always)
       │         │
       │    docker-build-push (needs lint-test)
       │    builds all 3 images; pushes to ACR on main only
       │         │
       │    terraform (needs docker-build-push)
       │    plan + PR comment [on PR]
       │    apply            [on main]
       │         │
       │    deploy (main only)
       │    az containerapp update --image
       │         │
       │    dast-scan (main only)
       │    ZAP baseline vs ACA FQDN
       │
       └─── nuget-publish (needs lint-test, main only, parallel)
            dotnet pack + push to GitHub Packages
```

CodeQL runs as a separate workflow (`codeql.yml`) on push/PR/weekly schedule.

---

## New Files Created

| File | Purpose |
|---|---|
| `.github/workflows/azure-dev.yml` | Full rewrite — 6-job Terraform-native pipeline |
| `.github/workflows/codeql.yml` | Dedicated C# SAST with weekly Monday 03:00 schedule |
| `docker/api/Dockerfile` | Multi-stage .NET 10 build; `aspnet:10.0` runtime; exposes ports 8080 + 8081 |
| `docker/worker/Dockerfile` | Multi-stage .NET 10 build; `aspnet:10.0` runtime; no exposed port |
| `terraform/modules/apps/main.tf` | `azurerm_container_app` for Api (external ingress, health probes) and Worker (no ingress) |
| `terraform/modules/apps/variables.tf` | Module inputs including `api_image`, `worker_image`, all connection strings |
| `terraform/modules/apps/outputs.tf` | `api_name`, `worker_name`, `api_fqdn` |
| `.zap/rules.tsv` | OWASP ZAP rule suppressions (empty; ready to populate) |

## Modified Files

| File | Change |
|---|---|
| `terraform/main.tf` | Added `module "apps"` after `module "keycloak"` |
| `terraform/variables.tf` | Added `api_image` and `worker_image` string variables |
| `terraform/outputs.tf` | Added `api_name`, `worker_name`, `api_fqdn` |
| `src/ToTen.Contracts/ToTen.Contracts.csproj` | Added `PackageId`, `Version`, `Authors`, `Description`, `GeneratePackageOnBuild=false` |

---

## Terraform `apps` Module Design

Mirrors the `keycloak` module pattern: `revision_mode = "Single"`, user-assigned managed identity, ACR `registry` block with admin credentials as a container-level secret.

**Api container app (`toten-prod-api`):**
- External ingress on port 8080 (HTTP)
- Liveness probe: `GET /health/alive` on port 8081, period 10s
- Readiness probe: `GET /health/ready` on port 8081, period 10s
- Secrets: `postgres-conn` (constructed from fqdn + password), `servicebus-conn`, `storage-conn`, `signalr-conn`, `appinsights-conn`
- Non-sensitive env: `AZURE_CLIENT_ID`, `Auth__Authority`, `KeyVault__Uri`, `SWAGGERUI_CLIENTID`

**Worker container app (`toten-prod-worker`):**
- No ingress (Service Bus consumer only)
- 1 replica (min and max)
- Secrets: `servicebus-conn`, `storage-conn`, `appinsights-conn`
- Non-sensitive env: `AZURE_CLIENT_ID`, `KeyVault__Uri`

---

## Pipeline Image Tag Strategy

| Image | Tag on push | Stable alias |
|---|---|---|
| `api/toten-api` | `sha-<git-sha>` | `latest` |
| `worker/toten-worker` | `sha-<git-sha>` | `latest` |
| `keycloak/toten-keycloak` | `latest` only | — |

SHA tags enable point-in-time rollback. The `az containerapp update --image sha-<sha>` step in `deploy` forces the exact revision deployed from each run.

---

## Required GitHub Actions Setup (Manual)

**New variable:**
| Name | Value |
|---|---|
| `ACR_NAME` | `totenprodacr` |

**Rename existing secrets:**
| Old name | New name |
|---|---|
| `AZURE_POSTGRES_PASSWORD` | `TF_VAR_POSTGRES_ADMIN_PASSWORD` |
| `AZURE_KEYCLOAK_PASSWORD` | `TF_VAR_KEYCLOAK_ADMIN_PASSWORD` |

Existing OIDC variables (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`) are unchanged.

---

## Multi-Env Extension Notes

When adding `staging` environment:
- Add `envs/staging.tfvars` with smaller SKUs
- The `api_image` / `worker_image` variables stay as pipeline inputs — no change to modules
- Consider separate Terraform workspaces per env (`terraform workspace new staging`)
- DAST `fail_action` can be `true` for staging (gate) and `false` for prod (monitor)
