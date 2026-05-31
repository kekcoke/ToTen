# ToTen — Deployment Readiness Report

**Date**: 2026-05-30  
**Reviewers**: Architect Agent · DevSecOps Agent · QA Engineer Agent  
**Branch**: `deploy/toten-provision`  
**Target environment**: Azure (canadacentral) — production

---

## Executive Summary

All three pipeline phases are code-complete. The codebase is blocked only by
manual Azure and GitHub configuration that cannot be automated without live
credentials. Once the steps below are executed, a single push to `main` will
drive the platform from zero infrastructure to a fully deployed, tested, and
monitored application.

| Phase | Code status | Gate blocking deployment |
|---|---|---|
| 3 — Terraform IaC | Complete | Terraform state backend not bootstrapped; `terraform init/validate` not run against real subscription |
| 4 — DevSecOps CI/CD | Complete | GitHub Actions variables/secrets not set; OIDC federation not configured |
| 5 — Quality Pipeline | Complete ✅ | None — 57/57 tests passing locally |

---

## Agent Findings

### Architect Agent — Phase 3

All nine Terraform modules (`container-apps`, `postgres`, `service-bus`,
`storage`, `registry`, `signalr`, `key-vault`, `keycloak`, `apps`,
`observability`) are present and wired in explicit dependency order in
`terraform/main.tf`. Secrets flow through `sensitive = true` variable blocks
and are stored as Key Vault secrets — no credentials are hardcoded in any
`.tf` file.

**Open items requiring live Azure access:**

- `terraform validate` has never been run against the bootstrap backend.
  The remote backend config in `main.tf` targets `toten-tfstate-rg` /
  `totentfstate` / `tfstate-prod`, which must exist before `terraform init`
  can succeed.
- `terraform plan -var-file=envs/prod.tfvars` has never produced a live plan.
- The custom Keycloak Docker image (`docker/keycloak/Dockerfile`) has not
  been built and verified locally — the `kc.sh build --db=postgres` step
  and realm file copy must succeed before the CI push step is trusted.

**Notable architecture decisions carried into deployment:**

- Region: `canadacentral` (set in `envs/prod.tfvars`)
- Resource group name: `toten-prod-rg` (derived from `locals.tf` prefix pattern)
- SKUs (prod.tfvars): Postgres `B_Standard_B2ms`, Service Bus `Standard`,
  SignalR `Free_F1`, ACR `Basic` — cost-optimised for initial provision

---

### DevSecOps Agent — Phase 4

The seven-job CI/CD pipeline (`azure-dev.yml`) is syntactically valid and
covers the full delivery chain: lint → Docker build/push → Terraform
plan/apply → ACA image update → ZAP DAST → Robot Framework → JMeter →
NuGet publish.

OIDC authentication (`ARM_USE_OIDC=true`, `azure/login@v2` with
`client-id`/`tenant-id`/`subscription-id`) is configured throughout. No
client secret is used — a federated credential on an Entra app registration
is required.

**Items that must be configured in GitHub before the pipeline can run:**

GitHub Actions **Variables** (Settings → Secrets and variables → Actions → Variables tab):

| Variable | Value |
|---|---|
| `AZURE_CLIENT_ID` | Client ID of the Entra app registration |
| `AZURE_TENANT_ID` | Azure AD tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Target Azure subscription ID |
| `ACR_NAME` | `totenprodacr` |

GitHub Actions **Secrets** (Settings → Secrets and variables → Actions → Secrets tab):

| Secret | Value |
|---|---|
| `TF_VAR_POSTGRES_ADMIN_PASSWORD` | Strong password for PostgreSQL admin |
| `TF_VAR_KEYCLOAK_ADMIN_PASSWORD` | Strong password for Keycloak admin |
| `ROBOT_API_KEY` | API key injected into Robot Framework acceptance tests |

**GitHub Advanced Security** must be enabled on the repository for CodeQL
results to appear in the Security → Code scanning tab (`codeql.yml` is
already wired).

---

### Entra ID Enterprise Application Architecture

**Question reviewed**: Given that ToTen runs its own Keycloak OIDC stack and will integrate with Microsoft services and 3rd-party applications on a new Azure subscription, should the Entra ID setup be a Group or Enterprise Application?

**Answer: Enterprise Application (App Registration), not a Group.**

Groups in Entra ID organize users and govern resource access — they do not create an application identity and cannot be targeted for OAuth2/OIDC federation. An App Registration (which auto-creates a service principal / Enterprise Application) is the correct construct because it provides:

- A service principal with OAuth2 / OIDC client credentials
- Delegated and application-level Microsoft Graph permissions (Teams, SharePoint, OneDrive, etc.)
- An OIDC discovery endpoint that Keycloak can consume as an upstream Identity Provider
- App Roles (defined in the manifest) that map to Keycloak realm roles
- Conditional Access policy targeting and consent auditing

**Two separate registrations are required** — the current doc creates only one:

| Registration | Purpose | Permissions |
|---|---|---|
| `toten-github-actions` | CI/CD OIDC (already in doc — Section C) | Contributor + KV Admin + Storage Blob Data Contributor |
| `toten-platform` | Application identity for Microsoft service and 3rd-party integration | Microsoft Graph scopes (e.g. `User.Read`, `Mail.Send`), App Roles in manifest |

Merging these into one registration violates least-privilege: the CI/CD pipeline does not need Graph access, and the platform application does not need Contributor on the subscription.

**Integration pattern with Keycloak:**

```
Users → Keycloak (primary IdP, realm ToTen)
           ↕  OIDC federation (Identity Provider config in Keycloak)
        Entra ID (upstream IdP — toten-platform app registration)
           ↕  delegated / application permissions
        Microsoft Graph / 3rd-party OIDC apps
```

Microsoft users SSO through Entra ID into Keycloak. Keycloak issues its own JWT (realm `ToTen`). The API receives and validates only Keycloak-issued tokens — no code changes are required to the API when adding the Entra ID federation.

---

#### Redirect URI for the Entra ID Enterprise Application

**The Swagger UI redirect URI does not go in Entra ID.** The Swagger UI (`/swagger/oauth2-redirect.html`) talks directly to Keycloak using the `ToTen-api-swagger` Keycloak client — Entra ID is not in that flow.

The redirect URI on the **`toten-platform` Entra ID app registration** is the **Keycloak OIDC broker callback endpoint**. After Entra ID authenticates a user, it redirects back to Keycloak at:

```
https://<keycloak_fqdn>/realms/ToTen/broker/<alias>/endpoint
```

Where:
- `<keycloak_fqdn>` — the ACA FQDN from `terraform output -raw keycloak_fqdn` after `terraform apply` completes (pattern: `keycloak.<revision>.<region>.azurecontainerapps.io`)
- `<alias>` — the alias given to the Entra ID Identity Provider when configured in Keycloak Admin UI; use `entra-id` as the canonical alias throughout this project

**URI type to select in Entra ID**: **Web** (server-side callback, not SPA/public client).

For the **`toten-github-actions` CI/CD registration**: no redirect URI. GitHub Actions OIDC federation authenticates via `subject` / `issuer` claim matching on the federated credential — there is no browser redirect involved.

**Additionally — production Keycloak client gap (see Known Issues):** The `ToTen-api-swagger` Keycloak client currently only allows `http://localhost:5082/swagger/oauth2-redirect.html`. The production ACA API FQDN redirect (`https://<api_fqdn>/swagger/oauth2-redirect.html`) must be added to that client before Swagger UI OAuth2 will work in production.

---

### QA Engineer Agent — Phase 5

Phase 5 is fully complete. All 57 integration tests pass (Categories,
Organizations, Users, Memberships, Storage, Manifests, Marketplace, SignalR
hub, security contract tests). Robot Framework, JMeter, and both CI jobs
(jobs 6 and 7) are wired.

**One known security gap** documented in `AuthorizationContractTests.cs`:
Items CRUD endpoints (`/items/*`) are currently public — `RequireAuthorization`
has not been applied to those routes. This does not block deployment but
should be resolved in the next sprint.

Robot Framework acceptance tests require a live `BASE_URL` (the ACA API
FQDN) and the `ROBOT_API_KEY` secret. Both are injected by the pipeline
after a successful `terraform apply` — no additional wiring needed.

---

## Pre-Deployment Checklist

Complete every item before pushing to `main` for the first time.

#### Bootstrap dependency order

Each row must be complete before the next begins.

| Step | Produces | Required by |
|---|---|---|
| A — Local tooling | `az` CLI, Terraform, Docker | All steps below |
| B.1 — Confirm subscription | `$SUBSCRIPTION_ID`, `$TENANT_ID` shell vars | B.2, B.4, C |
| B.2–B.3 — tfstate backend | `toten-tfstate-rg`, `totentfstate`, `tfstate-prod` | B.4, G (`terraform init`) |
| B.4 — Storage Blob role | SP can read/write tfstate | G (`terraform init`) |
| C — Entra app + federated creds | `$APP_ID`, `$SP_OBJ_ID`, OIDC trust | D, pipeline |
| D — GitHub variables/secrets | `ARM_*` vars, passwords | All pipeline jobs |
| E — GitHub Advanced Security | CodeQL results visible | Post-deploy only |
| F — Keycloak Docker image | Local image verified | Pipeline job 2 |
| G — terraform init/validate/plan | Backend connected, plan clean | Merge to `main` |
| H — `toten-platform` Entra app | Keycloak IdP federation | Post-first-deploy |

---

### A — Local tooling

- [X] Azure CLI installed and authenticated: `az login`
- [X] Terraform CLI ≥ 1.5 installed: `terraform version`
- [X] Docker daemon running (needed for Keycloak image verification)

### B — Bootstrap Terraform state backend (run once)

These resources must exist before `terraform init` can resolve the remote backend.
Complete all four sub-steps in order — later steps depend on variables set in B.1.

#### B.1 — Confirm active subscription and capture IDs

The new project subscription must be active before any resource is created. The shell
variables captured here are reused throughout B.2, B.4, and Section C.

```bash
# List all subscriptions — confirm the project subscription appears
az account list --output table

# Set the project subscription as active (use the Name or ID from the table above)
az account set --subscription "<your-toten-subscription-name-or-id>"

# Capture IDs — reused in B.4 and Section C
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)

echo "SUBSCRIPTION_ID: $SUBSCRIPTION_ID"
echo "TENANT_ID:       $TENANT_ID"
```

> **Keep this terminal open.** `$SUBSCRIPTION_ID` and `$TENANT_ID` are referenced in B.4
> and Section C. If you open a new shell, re-run `az account show` to re-export them.

- [X] Correct project subscription is active (`az account show` shows expected subscription)
- [X] `$SUBSCRIPTION_ID` captured
- [X] `$TENANT_ID` captured

#### B.2 — Create resource group and storage account


```bash
# Register the provider explicitly pointing to your variable
az provider register --namespace Microsoft.Storage --subscription $SUBSCRIPTION_ID

# (Optional) Wait 30 seconds and check progress until it says "Registered"
az provider show --namespace Microsoft.Storage --subscription $SUBSCRIPTION_ID --query "registrationState" --output table
```

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

> **Storage account name**: `totentfstate` must be globally unique across all of Azure.
> If the name is taken, choose an alternative and update `storage_account_name` in
> `terraform/main.tf` line 17.

- [X] Resource group `toten-tfstate-rg` created in `canadacentral`
- [X] Storage account `totentfstate` created (`Standard_LRS`, public blob access off)

#### B.3 — Create tfstate blob container

```bash
az storage container create \
  --name tfstate-prod \
  --account-name totentfstate \
  --subscription $SUBSCRIPTION_ID
```

- [X] Container `tfstate-prod` created inside `totentfstate`

#### B.4 — Grant Storage Blob Data Contributor to CI/CD service principal

> **Sequence note**: `$SP_OBJ_ID` is set in Section C step 2. Complete Section C steps 1 and 2
> first, then return here before continuing to C step 3.

This grants the `toten-github-actions` service principal read/write access to the tfstate
blob container. Without it, `terraform init` cannot authenticate to the backend.

```bash
# $SP_OBJ_ID must be set (from Section C step 2 — see below)
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

- [X] Storage Blob Data Contributor granted to `toten-github-actions` SP on `totentfstate`
  _(complete after Section C steps 1–2; return here before C step 3)_

### C — Create Entra app registration with OIDC federation

The pipeline uses OIDC — no long-lived client secret is stored anywhere.

> **Prerequisites**: `$SUBSCRIPTION_ID` and `$TENANT_ID` must be set (from Section B.1).
> After completing steps 1 and 2 below, pause and complete **Section B.4** before continuing to step 3.

```bash
# 1. Create the app registration
APP_ID=$(az ad app create --display-name "toten-github-actions" --query appId -o tsv)

# 2. Create a service principal for it
SP_OBJ_ID=$(az ad sp create --id $APP_ID --query id -o tsv)

# ── Pause: complete Section B.4 using $SP_OBJ_ID, then continue ──

# 3. Grant Contributor on the subscription (Terraform needs this to create all resources)
az role assignment create \
  --assignee $SP_OBJ_ID \
  --role Contributor \
  --scope /subscriptions/$SUBSCRIPTION_ID

# 4. Grant Key Vault Administrator (Terraform SP needs this to write secrets during apply)
az role assignment create \
  --assignee $SP_OBJ_ID \
  --role "Key Vault Administrator" \
  --scope /subscriptions/$SUBSCRIPTION_ID

# 5. Add federated credential for push to main
az ad app federated-credential create \
  --id $APP_ID \
  --parameters '{
    "name": "github-main",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:kekcoke/ToTen:ref:refs/heads/main",
    "audiences": ["api://AzureADTokenExchange"]
  }'

# 6. Add federated credential for pull requests
az ad app federated-credential create \
  --id $APP_ID \
  --parameters '{
    "name": "github-prs",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:kekcoke/ToTen:pull_request",
    "audiences": ["api://AzureADTokenExchange"]
  }'

# Print values needed for GitHub
echo "AZURE_CLIENT_ID:       $APP_ID"
echo "AZURE_TENANT_ID:       $TENANT_ID"
echo "AZURE_SUBSCRIPTION_ID: $SUBSCRIPTION_ID"
```

> **Note**: Replace `kekcoke/ToTen` with your actual GitHub `owner/repo` slug
> in both federated credential subjects if it differs.

- [X] App registration `toten-github-actions` created
- [X] Service principal created (complete Section B.4 before continuing)
- [X] Service principal has Contributor role on subscription
- [X] Service principal has Key Vault Administrator role on subscription
- [X] Federated credential for `refs/heads/main` added
- [X] Federated credential for `pull_request` added
- [X] `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` values recorded

### D — Configure GitHub Actions variables and secrets

In the GitHub repository: **Settings → Secrets and variables → Actions**

**Variables tab:**

| Variable | Value |
|---|---|
| `AZURE_CLIENT_ID` | Output from step C |
| `AZURE_TENANT_ID` | Output from step C |
| `AZURE_SUBSCRIPTION_ID` | Output from step C |
| `ACR_NAME` | `totenprodacr` |

**Secrets tab:**

| Secret | Notes |
|---|---|
| `TF_VAR_POSTGRES_ADMIN_PASSWORD` | Min 8 chars, must include uppercase, lowercase, number, symbol |
| `TF_VAR_KEYCLOAK_ADMIN_PASSWORD` | Same complexity requirements |
| `ROBOT_API_KEY` | Any non-empty string for now; swap for a real Keycloak-issued API key after first deploy |

- [X] All four variables set
- [X] All three secrets set ()

### E — Enable GitHub Advanced Security

**Settings → Code security → GitHub Advanced Security → Enable**

- [X] GitHub Advanced Security enabled (required for CodeQL results to surface) (looks enabled)

### F — Verify Keycloak Docker image builds locally

```bash
# From repo root
docker build -f docker/keycloak/Dockerfile -t toten-keycloak:local .

# Confirm realm file was baked in (should print the file path)
docker run --rm toten-keycloak:local ls /opt/keycloak/data/import/
```

If above doesn't work, it's because of modern Keycloak Docker images are set with a hardcoded ENTRYPOINT that
points straight to the management script (/opt/keycloak/bin/kc.sh). Command translation comes 
`/opt/keycloak/bin/kc.sh ls /opt/keycloak/data/import/` which ls is not translated. It then suggests `kc.tools`. 
Run commands below:

```bash
docker run --rm \
  --entrypoint /bin/sh \
  toten-keycloak:local \
  -c "ls -la /opt/keycloak/data/import/"
```

Confirm the realm file baked into the image matches the current source file (run before every `terraform apply` or ACR push):

```bash
SOURCE_HASH=$(shasum -a 256 src/ToTen.AppHost/realms/ToTen-realm.json | awk '{print $1}')
IMAGE_HASH=$(docker run --rm --entrypoint /bin/sh toten-keycloak:local \
  -c "sha256sum /opt/keycloak/data/import/ToTen-realm.json" | awk '{print $1}')

echo "Source: $SOURCE_HASH"
echo "Image:  $IMAGE_HASH"
[ "$SOURCE_HASH" = "$IMAGE_HASH" ] && echo "OK — image is current" || echo "MISMATCH — rebuild image"
```

If `MISMATCH`, re-run the `docker build` above before continuing.

- [X] Image builds without error
- [X] `ToTen-realm.json` appears in `/opt/keycloak/data/import/` inside the image
- [X] Realm hash matches source (`OK — image is current`)

### H — Create `toten-platform` Entra ID app registration

This is separate from the CI/CD service principal (`toten-github-actions`) and must not be merged with it.

> **Prerequisite for steps 4–5**: `terraform apply` must have completed (Deployment Step 1–2) before
> the Keycloak FQDN is available. Steps 1–3 can be run at any time after Section C.

```bash
# 1. Create the platform app registration
PLATFORM_APP_ID=$(az ad app create --display-name "toten-platform" --query appId -o tsv)

# 2. Create service principal
az ad sp create --id $PLATFORM_APP_ID

# 3. Define App Roles in the manifest (edit the JSON then update)
#    Add roles: user, business_owner, internal_user, admin — matching ToTen Keycloak realm roles
az ad app update --id $PLATFORM_APP_ID \
  --app-roles '[
    {"allowedMemberTypes":["User"],"description":"Standard individual user","displayName":"User","id":"00000001-0000-0000-0000-000000000001","isEnabled":true,"value":"user"},
    {"allowedMemberTypes":["User"],"description":"Owner of a business organization","displayName":"BusinessOwner","id":"00000001-0000-0000-0000-000000000002","isEnabled":true,"value":"business_owner"},
    {"allowedMemberTypes":["User"],"description":"ToTen internal staff","displayName":"InternalUser","id":"00000001-0000-0000-0000-000000000003","isEnabled":true,"value":"internal_user"},
    {"allowedMemberTypes":["User"],"description":"Realm administrator","displayName":"Admin","id":"00000001-0000-0000-0000-000000000004","isEnabled":true,"value":"admin"},
    {"allowedMemberTypes":["User"],"description":"Global system administrator","displayName":"SuperAdmin","id":"00000001-0000-0000-0000-000000000005","isEnabled":true,"value":"super_admin"},
    {"allowedMemberTypes":["User","Application"],"description":"Third-party integration service","displayName":"ThirdParty","id":"00000001-0000-0000-0000-000000000006","isEnabled":true,"value":"third_party"}
  ]'

# 4. Get the Keycloak FQDN from Terraform state (run AFTER terraform apply completes)
KEYCLOAK_FQDN=$(cd terraform && terraform output -raw keycloak_fqdn)
echo "KEYCLOAK_FQDN: $KEYCLOAK_FQDN"

# 5. Add the Keycloak OIDC broker callback as the redirect URI (Web type)
#    Alias is fixed as "entra-id" — must match the alias set in Keycloak Admin UI below
az ad app update --id $PLATFORM_APP_ID \
  --web-redirect-uris "https://${KEYCLOAK_FQDN}/realms/ToTen/broker/entra-id/endpoint"

echo "PLATFORM_APP_ID (client_id for Keycloak IdP config): $PLATFORM_APP_ID"
echo "TENANT_ID: $(az account show --query tenantId -o tsv)"
```

Then in Keycloak Admin UI (`https://${KEYCLOAK_FQDN}/admin/master/console/#/ToTen/identity-providers`):

- Add Identity Provider → **OpenID Connect v1.0**
- Alias: **`entra-id`** (must match the alias used in step 5 above)
- Discovery URL: `https://login.microsoftonline.com/<TENANT_ID>/v2.0/.well-known/openid-configuration`
- Client ID: `$PLATFORM_APP_ID`
- Client Secret: generate one via `az ad app credential reset --id $PLATFORM_APP_ID`

**Add a Role Mapper in Keycloak** so incoming Entra ID role claims are mapped to Keycloak realm roles:

- In the Identity Provider config, go to **Mappers → Add mapper**
- Type: **Role Importer**
- Sync mode: **Inherit** (or Force)
- The `value` strings in the App Roles above match Keycloak realm role names exactly — no manual name translation is needed.

- [X] `toten-platform` app registration created
- [X] App Roles (user, business_owner, internal_user, admin) defined in manifest
- [ ] Redirect URI `https://${KEYCLOAK_FQDN}/realms/ToTen/broker/entra-id/endpoint` added (Web type)
- [ ] Keycloak Identity Provider configured pointing at Entra ID OIDC discovery endpoint
- [ ] `PLATFORM_APP_ID` and `TENANT_ID` recorded for Keycloak IdP config
- [ ] Role Mapper (Role Importer type) added to the Entra ID Identity Provider in Keycloak

> **No EF Core migration role seeding required.** The six application roles (`user`, `business_owner`,
> `internal_user`, `admin`, `super_admin`, `third_party`) are Keycloak JWT claims — they live entirely
> in Keycloak, not in the application database. The only seeded DB data is Categories (5 rows) and
> demo InventoryItems (3 rows). `OrganizationMemberships.Role` is a separate org-level membership
> field (default `"Member"`) unrelated to these realm roles.

---

### G — Run Terraform locally against real Azure

> **Local run prerequisite**: Export `ARM_CLIENT_ID`, `ARM_TENANT_ID`, `ARM_SUBSCRIPTION_ID`,
> and `ARM_USE_OIDC=true` in your shell before running `terraform init` locally. These are
> the values captured in B.1 and printed at the end of Section C.

```bash
cd terraform

# Initialise — connects to the remote backend in Azure
terraform init

# Validate module syntax against provider schema
terraform validate

# Use the SHA from main — CI only pushes images on merges to main.
# git rev-parse HEAD would give the feature-branch SHA which has no ACR image.
API_IMAGE="totenprodacr.azurecr.io/api/toten-api:sha-$(git rev-parse origin/main)"
WORKER_IMAGE="totenprodacr.azurecr.io/worker/toten-worker:sha-$(git rev-parse origin/main)"

# Preview the full infrastructure delta (requires passwords)
terraform plan \
  -var-file="envs/prod.tfvars" \
  -var="postgres_admin_password=<your-password>" \
  -var="keycloak_admin_password=<your-password>" \
  -var="api_image=${API_IMAGE}" \
  -var="worker_image=${WORKER_IMAGE}"
```

> For `terraform plan` the image does not need to exist in ACR — Terraform only validates the plan.
> For `terraform apply` the image must be present in ACR; in CI this is guaranteed because the
> `docker-build-push` job runs before the `terraform` job and passes the sha-tagged URI directly.

- [ ] `terraform init` succeeds (backend connected)
- [ ] `terraform validate` reports no errors
- [ ] `terraform plan` generates a clean plan with expected resources (no errors; expected ~40–60 resources to add on first run)

---

## Deployment Steps

With all pre-deployment items checked, trigger the full pipeline by pushing
to `main`.

### Step 1 — First pipeline run (push to main)

```bash
git checkout main
git merge deploy/toten-provision
git push origin main
```

The seven-job pipeline will execute in order:

1. `lint-test` — 57 integration tests run against ephemeral Testcontainers Postgres
2. `docker-build-push` — API, Worker, Keycloak images built and pushed to ACR
3. `terraform` — `init` + `validate` + `plan` then `apply`; creates all Azure resources
4. `deploy` — ACA container apps updated with the sha-tagged images from job 2
5. `dast-scan` — OWASP ZAP baseline runs against the live ACA API FQDN
6. `robot-tests` — Robot Framework acceptance suite runs against live ACA
7. `performance-test` — JMeter baseline load test artifact generated
8. `nuget-publish` — `ToTen.Contracts` pushed to GitHub Packages

Monitor progress at: **GitHub → Actions → CI/CD**

### Step 2 — Verify Terraform outputs (after apply)

Once job 3 completes, capture the outputs:

```bash
cd terraform
terraform output
```

Expected outputs: `api_fqdn`, `api_name`, `worker_name`, `resource_group_name`,
`keycloak_fqdn`, `acr_login_server`, `key_vault_uri`.

### Step 3 — Verify Keycloak realm import

```bash
# Replace <keycloak_fqdn> with the value from terraform output
curl -s https://<keycloak_fqdn>/realms/ToTen | jq .realm
# Expected output: "ToTen"
```

### Step 4 — Run EF Core migrations against the live database

The database is provisioned by Terraform but schema migrations must be applied
manually on first deploy.

```bash
# Set the connection string (retrieve from Key Vault or Terraform output)
export ConnectionStrings__ToTenDB="Host=<postgres_fqdn>;Database=ToTen;Username=adminuser;Password=<password>"

dotnet ef database update \
  --project src/ToTen.Api \
  --startup-project src/ToTen.Api
```

### Step 5 — Smoke test the live API

```bash
API_URL="https://<api_fqdn>"

# Health check
curl -s "$API_URL/health/alive"   # expected: Healthy
curl -s "$API_URL/health/ready"   # expected: Healthy

# OpenAPI spec (unauthenticated)
curl -s "$API_URL/swagger/v1/swagger.json" | jq .info.title
```

---

## Post-Deployment Checklist

- [ ] GitHub Actions run completes all 8 jobs green (jobs 6/7 are `continue-on-error` — check artifacts even if marked yellow)
- [ ] `terraform output api_fqdn` returns a valid FQDN
- [ ] `GET /realms/ToTen` on Keycloak FQDN returns HTTP 200 with `"realm": "ToTen"`
- [ ] EF Core migrations applied — no pending migrations (`dotnet ef migrations list` shows all applied)
- [ ] `GET /health/alive` and `/health/ready` both return `Healthy`
- [ ] Swagger UI accessible via `https://<api_fqdn>/swagger`
- [ ] ZAP DAST report artifact downloaded and reviewed (no Critical findings)
- [ ] Robot Framework artifact downloaded — review pass/fail counts
- [ ] JMeter HTML report artifact downloaded — confirm baseline throughput recorded
- [ ] `ToTen.Contracts` NuGet package visible in GitHub Packages registry
- [ ] CodeQL results appear in Security → Code scanning tab
- [ ] Application Insights receiving telemetry: check Azure portal → Application Insights → Live Metrics

---

## Known Issues to Address Post-Deployment

| Issue | Location | Priority |
|---|---|---|
| Items CRUD endpoints (`/items/*`) are public — `RequireAuthorization` missing | `Features/Items/*/` endpoints | High |
| `ToTen-api-swagger` Keycloak client only allows `http://localhost:5082/swagger/oauth2-redirect.html` — production ACA redirect URI not registered; Swagger UI OAuth2 login will fail until `https://<api_fqdn>/swagger/oauth2-redirect.html` is added to `redirectUris` and `webOrigins` | `src/ToTen.AppHost/realms/ToTen-realm.json` (lines 752–753) | High — blocks Swagger UI auth in production |
| `toten-platform` Entra ID app registration not yet created — required before Keycloak Identity Provider federation with Entra ID can be configured | Section H of pre-deployment checklist | High (if Microsoft/3rd-party integration is in scope for first deploy) |
| `ROBOT_API_KEY` is a placeholder — swap for a real Keycloak-issued token | GitHub secret + Robot Framework tests | Medium |
| `allowed_cidr_ranges` in `prod.tfvars` is empty `[]` — GitHub Actions runner IPs not whitelisted for Postgres | `terraform/envs/prod.tfvars` | Medium (only blocks direct DB access from CI; app connects via ACA) |
| `totentfstate` storage account name may be taken (globally unique constraint) | `terraform/main.tf` line 17 | Low — verify during bootstrap |
