# Phase 3 — Multi-Environment Extension Notes
**Date**: 2026-05-29
**Status**: Reference (not active for current prod-only implementation)

The live Phase 3 implementation targets a single `prod` environment. This document records how to extend the Terraform layout to support `dev`, `staging`, and `prod` when needed.

---

## Recommended Strategy: `tfvars` Files per Environment (not Workspaces)

Terraform workspaces share the same module code but maintain separate state files within a single backend container. `tfvars` files give the same isolation while keeping state in clearly named containers, making it obvious which file corresponds to which environment.

**Preferred layout:**

```
/terraform/
├── envs/
│   ├── prod.tfvars          # Active — values for production
│   ├── staging.tfvars       # Add when staging is needed
│   └── dev.tfvars           # Add when dev environment is needed
├── main.tf
├── variables.tf
└── ...
```

**Backend configuration per environment:**

Each environment gets its own container in the dedicated `toten-prod-tfstate` storage account (or a renamed `toten-tfstate` account if managing multiple envs):

```hcl
# prod
terraform {
  backend "azurerm" {
    resource_group_name  = "toten-tfstate-rg"
    storage_account_name = "totentfstate"
    container_name       = "tfstate-prod"
    key                  = "toten.prod.terraform.tfstate"
  }
}
```

For `staging` and `dev`, use `container_name = "tfstate-staging"` / `"tfstate-dev"` and matching `key` values. Pass the backend config at init time:

```bash
terraform init -backend-config="container_name=tfstate-staging" -backend-config="key=toten.staging.terraform.tfstate"
```

---

## Naming Convention

All resources follow: `toten-{env}-{resource-type}`.

The `locals.tf` `env` input variable drives this:

```hcl
locals {
  prefix = "toten-${var.environment}"
}
```

| Resource type | prod name | staging name | dev name |
|---|---|---|---|
| Resource group | `toten-prod-rg` | `toten-staging-rg` | `toten-dev-rg` |
| ACA environment | `toten-prod-cae` | `toten-staging-cae` | `toten-dev-cae` |
| PostgreSQL server | `toten-prod-postgres` | `toten-staging-postgres` | `toten-dev-postgres` |
| Key Vault | `toten-prod-kv` | `toten-staging-kv` | `toten-dev-kv` |
| ACR | `toten-prod-acr` | `toten-staging-acr` | `toten-dev-acr` |

---

## Per-Environment `tfvars` Structure

```hcl
# envs/staging.tfvars
environment             = "staging"
location                = "eastus"
postgres_sku            = "B_Standard_B1ms"   # cheaper SKU for staging
service_bus_sku         = "Standard"
signalr_sku             = "Standard_S1"
acr_sku                 = "Basic"
allowed_cidr_ranges     = ["<staging-runner-ip>/32"]
```

```hcl
# envs/prod.tfvars
environment             = "prod"
location                = "eastus"
postgres_sku            = "GP_Standard_D2s_v3"
service_bus_sku         = "Standard"
signalr_sku             = "Standard_S1"
acr_sku                 = "Standard"
allowed_cidr_ranges     = ["<prod-runner-ip>/32"]
```

Apply with:
```bash
terraform apply -var-file="envs/staging.tfvars"
```

---

## GitHub Actions Integration (future Phase 4 extension)

Add a `matrix` strategy to the CI workflow to deploy to staging first, gate on smoke tests, then promote to prod:

```yaml
strategy:
  matrix:
    environment: [staging, prod]
```

Gate the `prod` job on the `staging` job completing successfully and a manual approval review in GitHub Environments.

---

## SKU Recommendations by Environment

| Module | dev | staging | prod |
|---|---|---|---|
| PostgreSQL | `B_Standard_B1ms` | `B_Standard_B2ms` | `GP_Standard_D2s_v3` |
| Service Bus | `Standard` | `Standard` | `Standard` |
| SignalR | `Standard_S1` | `Standard_S1` | `Standard_S1` |
| ACR | `Basic` | `Basic` | `Standard` |
| Key Vault (soft delete retention) | 7 days | 7 days | 90 days |

---

## What Changes vs. the Single-Env Implementation

- `locals.tf`: `prefix = "toten-${var.environment}"` — no change needed, already parameterized.
- `modules/postgres/main.tf`: `sku_name` becomes a variable rather than a hardcoded value.
- `modules/key-vault/main.tf`: `soft_delete_retention_days` varies by env (7 for non-prod, 90 for prod).
- Root `main.tf`: backend block uses environment-specific container name (passed via `-backend-config` at init time).
- GitHub Actions: secrets scoped to GitHub Environment (`production`, `staging`) rather than repo-level.
