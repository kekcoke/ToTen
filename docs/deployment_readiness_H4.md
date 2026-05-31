# Deployment Readiness — Section H.4: Keycloak FQDN Output + Backend Bootstrap

**Date**: 2026-05-30  
**POC**: Cloud Architect Agent (AdaL)  
**TL;DR**: Two blockers must be resolved before `terraform init` succeeds and the Entra ID ↔ Keycloak integration works: (1) add a missing `keycloak_fqdn` root output to `terraform/outputs.tf`, and (2) bootstrap the Azure Storage backend manually via CLI because Terraform cannot create its own state storage.

---

## 1. Missing `keycloak_fqdn` Root Output

### Problem

Section H step 4 of `deployment-readiness.md` references `terraform output -raw keycloak_fqdn`, but this output **does not exist** in the root `terraform/outputs.tf`.

The `keycloak` module correctly exports the FQDN:

```hcl
# terraform/modules/keycloak/outputs.tf (exists ✅)
output "fqdn" {
  value = azurerm_container_app.keycloak.latest_revision_fqdn
}
```

But the root only surfaces the **authority URL** (which includes path segments):

```hcl
# terraform/outputs.tf line 21-23
output "keycloak_authority_url" {
  value = module.keycloak.authority_url    # = "https://<fqdn>/realms/ToTen"
}
# ❌ No raw keycloak_fqdn output
```

The Entra ID redirect URI in Section H.5 needs the **raw FQDN** (no path), as does the discovery URL construction in Keycloak Admin UI.

### Fix

Add this output block to `terraform/outputs.tf` (after line 23, alongside the existing `keycloak_authority_url`):

```hcl
output "keycloak_fqdn" {
  value       = module.keycloak.fqdn
  description = "Keycloak Container App FQDN — used for Entra ID redirect URI (Section H.5) and IdP endpoint construction."
}
```

**What changes in deployment-readiness.md Section H**: After this fix, step H.4 (`terraform output -raw keycloak_fqdn`) will work correctly. No other changes needed — the `keycloak_authority_url` output (used by `apps` module) remains unchanged.

### Why the FQDN Matters

The Entra ID redirect URI must be the **Keycloak broker callback endpoint**:

```
https://<keycloak_fqdn>/realms/ToTen/broker/entra-id/endpoint
```

| Variable | Source | Example value |
|---|---|---|
| `<keycloak_fqdn>` | `terraform output -raw keycloak_fqdn` | `toten-prod-keycloak.proud-mushroom-01.canadacentral.azurecontainerapps.io` |
| `entra-id` | Canonical alias (fixed) | `entra-id` |

---

## 2. Backend Bootstrap: Manual CLI Is Required

### The Chicken-and-Egg Problem

The `backend "azurerm"` block in `terraform/main.tf` (lines 15–20) tells Terraform where to store state:

```hcl
backend "azurerm" {
  resource_group_name  = "toten-tfstate-rg"
  storage_account_name = "totentfstate"
  container_name       = "tfstate-prod"
  key                  = "toten.prod.terraform.tfstate"
}
```

But Terraform reads this configuration **during `init`** — before it can plan or apply anything. The storage account must already exist:

```
terraform init
  → reads main.tf
    → sees backend "azurerm" → tries to connect
      → ❌ Storage account doesn't exist → init fails
```

**This is universal**: Terraform can never create its own remote state backend. The same pattern applies to S3, GCS, and all other remote backends.

### Bootstrap Steps (from `deployment-readiness.md` Section B)

Run these **once** before the first `terraform init`. They're already documented in full in the main readiness doc — reproduced here for clarity with the explicit dependency order.

#### Prerequisites

- Azure CLI installed and authenticated (`az login`)
- Target subscription is active
- `$SUBSCRIPTION_ID` and `$TENANT_ID` captured:

```bash
az account set --subscription "<your-toten-subscription-name-or-id>"
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)
echo "SUBSCRIPTION_ID: $SUBSCRIPTION_ID"
echo "TENANT_ID:       $TENANT_ID"
```

#### B.2 — Create resource group and storage account

```bash
az group create \
  --name toten-tfstate-rg \
  --location canadacentral \
  --subscription $SUBSCRIPTION_ID

az storage account create \
  --name totentfstate \
  --resource-group toten-tfstate-rg \
  --location canadacentral \
  --subscription $SUBSCRIPTION_ID \
  --sku Standard_LRS \
  --allow-blob-public-access false
```

> ⚠️ `totentfstate` must be globally unique across all of Azure. If taken, pick a new name and update `storage_account_name` in `terraform/main.tf` line 17.

#### B.3 — Create tfstate blob container

```bash
az storage container create \
  --name tfstate-prod \
  --account-name totentfstate \
  --subscription $SUBSCRIPTION_ID
```

#### B.4 — Grant service principal access to the storage account

> **Prerequisite**: The Entra app registration + service principal from Section C must exist. After creating it, return here.

```bash
# $SP_OBJ_ID comes from Section C step 2
TFSTATE_ID=$(az storage account show \
  --name totentfstate \
  --resource-group toten-tfstate-rg \
  --subscription $SUBSCRIPTION_ID \
  --query id -o tsv)

az role assignment create \
  --assignee $SP_OBJ_ID \
  --role "Storage Blob Data Contributor" \
  --scope $TFSTATE_ID
```

#### After Bootstrap — `terraform init` Succeeds

```bash
cd terraform
export ARM_CLIENT_ID="<from Section C>"
export ARM_TENANT_ID="$TENANT_ID"
export ARM_SUBSCRIPTION_ID="$SUBSCRIPTION_ID"
export ARM_USE_OIDC=true

terraform init          # ✅ Backend connected
terraform validate      # ✅ No errors
terraform plan \
  -var-file="envs/prod.tfvars" \
  -var="postgres_admin_password=<pw>" \
  -var="keycloak_admin_password=<pw>" \
  -var="api_image=placeholder/api:latest" \
  -var="worker_image=placeholder/worker:latest"
```

---

## 3. Consolidated Execution Order

| Step | Action | Location | Depends on |
|------|--------|----------|------------|
| A | Local tooling (`az`, `terraform`, `docker`) | Any | — |
| B.1 | Capture `$SUBSCRIPTION_ID`, `$TENANT_ID` | Any | A |
| C.1–2 | Create `toten-github-actions` app + SP | Any | B.1 |
| **B.2** | **Create `toten-tfstate-rg` + `totentfstate`** | Any | B.1 |
| **B.3** | **Create `tfstate-prod` container** | Any | B.2 |
| **B.4** | **Grant Storage Blob Data Contributor to SP** | Any | C.2, B.2 |
| C.3–6 | Grant Contributor + KV Admin, add federated creds | Any | C.2 |
| D | GitHub variables + secrets | GitHub UI | C |
| **Fix 1** | **Add `keycloak_fqdn` output to `terraform/outputs.tf`** | `terraform/` | — |
| **G** | **`terraform init` → `validate` → `plan`** | `terraform/` | B.4, Fix 1 |

---

## 4. Verification Checklist

- [x] `keycloak_fqdn` output added to `terraform/outputs.tf`
- [x] `toten-tfstate-rg` resource group exists in `canadacentral`
- [x] `totentfstate` storage account exists (Standard_LRS, public access off)
- [x] `tfstate-prod` blob container exists
- [x] Storage Blob Data Contributor role assigned to `toten-github-actions` SP
- [x] `terraform init` succeeds from `terraform/` directory
- [x] `terraform validate` reports "Success! The configuration is valid."
- [x] `terraform plan` generates a clean plan
- [ ] `terraform output -raw keycloak_fqdn` returns a valid FQDN (after `apply`)
