#!/usr/bin/env bash
# scripts/toten.sh — ToTen infrastructure lifecycle CLI
# Usage: ./scripts/toten.sh <command> [--profile prod|free-tier] [--yes] [--dry-run]
set -euo pipefail

# ── Constants ─────────────────────────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TF_DIR="$REPO_ROOT/terraform"
TF_STATE_RG="toten-tfstate-rg"
TF_STATE_ACCOUNT="totentfstate"
TF_STATE_CONTAINER="tfstate-prod"
TF_STATE_BLOB="toten.prod.terraform.tfstate"
RESOURCE_GROUP="toten-prod-rg"
ACR_NAME="totenprodacr"
ENTRA_APP_NAME="toten-github-actions"
GITHUB_REPO="${GITHUB_REPOSITORY:-kekcoke/ToTen}"

REQUIRED_PROVIDERS=(
  "Microsoft.App"
  "Microsoft.SignalRService"
  "Microsoft.ContainerRegistry"
  "Microsoft.DBforPostgreSQL"
  "Microsoft.ServiceBus"
  "Microsoft.Storage"
  "Microsoft.KeyVault"
  "Microsoft.ManagedIdentity"
  "Microsoft.OperationalInsights"
  "Microsoft.Insights"
)

# ── Runtime state ─────────────────────────────────────────────────────────────
PROFILE="prod"
YES=false
DRY_RUN=false
COMMAND=""

# ── Colors ────────────────────────────────────────────────────────────────────
RED='\033[0;31m'
YELLOW='\033[1;33m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

# ── Helpers ───────────────────────────────────────────────────────────────────
log_info()  { echo -e "${CYAN}[INFO]${NC}  $*"; }
log_ok()    { echo -e "${GREEN}[ OK ]${NC}  $*"; }
log_warn()  { echo -e "${YELLOW}[WARN]${NC}  $*"; }
log_error() { echo -e "${RED}[ERR ]${NC}  $*" >&2; }
die()       { log_error "$*"; exit 1; }

# Print and optionally execute a command; dry-run echoes it instead
run_cmd() {
  if [[ "$DRY_RUN" == true ]]; then
    echo -e "${YELLOW}[DRY-RUN]${NC} $(printf '%q ' "$@")"
    return 0
  fi
  "$@"
}

# Prompt for yes/no; returns 0 on yes. Bypassed by --yes.
confirm() {
  local prompt="${1:-Continue?}"
  if [[ "$YES" == true ]]; then return 0; fi
  echo -en "${BOLD}${prompt}${NC} [y/N] "
  local reply; read -r reply
  [[ "$reply" =~ ^[Yy]$ ]]
}

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || die "Required command not found: $1. Install it before continuing."
}

# ── Pre-flight checks ─────────────────────────────────────────────────────────
check_az_login() {
  az account show --output none 2>/dev/null \
    || die "Not logged in to Azure CLI. Run: az login"
  log_ok "Azure CLI authenticated"
}

check_subscription() {
  local sub
  sub=$(az account show --query id -o tsv)
  log_info "Active subscription: $sub"
}

# Verify all 10 resource providers are registered (guards Error 1 from troubleshooting docs)
check_providers() {
  log_info "Checking resource provider registrations..."
  local missing=()
  for ns in "${REQUIRED_PROVIDERS[@]}"; do
    local state
    state=$(az provider show --namespace "$ns" --query registrationState -o tsv 2>/dev/null || echo "Unknown")
    if [[ "$state" != "Registered" ]]; then
      missing+=("$ns ($state)")
    fi
  done
  if [[ ${#missing[@]} -gt 0 ]]; then
    log_error "Unregistered providers: ${missing[*]}"
    die "Run './scripts/toten.sh bootstrap' to register providers before provisioning."
  fi
  log_ok "All ${#REQUIRED_PROVIDERS[@]} resource providers registered"
}

check_secrets_tfvars() {
  local secrets="$TF_DIR/envs/secrets.tfvars"
  [[ -f "$secrets" ]] || die "Missing $secrets.\nCreate it with:\n  postgres_admin_password = \"<pw>\"\n  keycloak_admin_password = \"<pw>\"\n  api_image    = \"${ACR_NAME}.azurecr.io/api/toten-api:sha-<sha>\"\n  worker_image = \"${ACR_NAME}.azurecr.io/worker/toten-worker:sha-<sha>\""
}

# Detect Failed-state container apps and offer to delete them (guards Error 4)
check_failed_container_apps() {
  local failed_apps
  failed_apps=$(az containerapp list \
    --resource-group "$RESOURCE_GROUP" \
    --query "[?properties.provisioningState=='Failed'].name" \
    -o tsv 2>/dev/null || echo "")
  if [[ -z "$failed_apps" ]]; then
    log_ok "No Failed container apps found"; return 0
  fi
  log_warn "Failed-state container apps: $failed_apps"
  log_warn "Terraform cannot import Failed apps — they must be deleted first."
  confirm "Delete these Failed container apps now?" \
    || die "Aborting. Delete them manually: az containerapp delete --name <name> --resource-group $RESOURCE_GROUP --yes"
  for app in $failed_apps; do
    run_cmd az containerapp delete --name "$app" --resource-group "$RESOURCE_GROUP" --yes
    log_ok "Deleted $app"
  done
}

# ── Terraform helpers ─────────────────────────────────────────────────────────
tf() {
  (cd "$TF_DIR" && run_cmd terraform "$@")
}

tf_with_vars() {
  (cd "$TF_DIR" && run_cmd terraform "$@" \
    -var-file="envs/${PROFILE}.tfvars" \
    -var-file="envs/secrets.tfvars")
}

# Read a value from secrets.tfvars
read_secret() {
  local key="$1"
  grep "^${key}" "$TF_DIR/envs/secrets.tfvars" 2>/dev/null \
    | sed 's/.*= *"\(.*\)"/\1/' || echo ""
}

# Write or update a key in secrets.tfvars
write_secret() {
  local key="$1" value="$2"
  local secrets="$TF_DIR/envs/secrets.tfvars"
  if grep -q "^${key}" "$secrets" 2>/dev/null; then
    sed -i.bak "s|^${key}.*|${key} = \"${value}\"|" "$secrets"
    rm -f "$secrets.bak"
  else
    echo "${key} = \"${value}\"" >> "$secrets"
  fi
}

# ── Smoke tests ───────────────────────────────────────────────────────────────
_run_smoke_tests() {
  local api_fqdn="${1:-}" keycloak_fqdn="${2:-}"
  log_info "Running smoke tests..."
  if [[ -n "$api_fqdn" ]]; then
    local api_title
    api_title=$(curl -sf --max-time 15 "https://${api_fqdn}/openapi/v1.json" \
      | jq -r '.info.title' 2>/dev/null || echo "")
    if [[ "$api_title" == "ToTen API" ]]; then
      log_ok "API smoke test passed ($api_fqdn)"
    else
      log_warn "API smoke test: unexpected response '${api_title:-none}' — app may still be starting"
    fi
  fi
  if [[ -n "$keycloak_fqdn" ]]; then
    local realm
    realm=$(curl -sf --max-time 20 "https://${keycloak_fqdn}/realms/ToTen" \
      | jq -r '.realm' 2>/dev/null || echo "")
    if [[ "$realm" == "ToTen" ]]; then
      log_ok "Keycloak smoke test passed (realm ToTen)"
    else
      log_warn "Keycloak smoke test: realm '${realm:-no response}' — JVM may be cold-starting (~60s)"
    fi
  fi
}

# Load Terraform outputs from saved outputs.env
_load_tf_outputs() {
  local outputs_file="$TF_DIR/outputs.env"
  [[ -f "$outputs_file" ]] && source "$outputs_file" || true
}

# ── cmd: bootstrap ────────────────────────────────────────────────────────────
cmd_bootstrap() {
  require_cmd az; require_cmd terraform; require_cmd jq

  log_info "=== BOOTSTRAP ==="

  check_az_login

  # Set subscription
  local subscription_id tenant_id
  if [[ -n "${AZURE_SUBSCRIPTION_ID:-}" ]]; then
    subscription_id="$AZURE_SUBSCRIPTION_ID"
  else
    az account list --output table
    echo -n "Enter subscription name or ID to use: "
    read -r subscription_id
  fi
  run_cmd az account set --subscription "$subscription_id"
  subscription_id=$(az account show --query id -o tsv)
  tenant_id=$(az account show --query tenantId -o tsv)
  log_ok "Subscription: $subscription_id  Tenant: $tenant_id"

  # Register all 10 resource providers (guards Error 1)
  log_info "Registering resource providers..."
  for ns in "${REQUIRED_PROVIDERS[@]}"; do
    local state
    state=$(az provider show --namespace "$ns" --query registrationState -o tsv 2>/dev/null || echo "NotRegistered")
    if [[ "$state" == "Registered" ]]; then
      log_ok "$ns already Registered"
    else
      run_cmd az provider register --namespace "$ns" --subscription "$subscription_id"
      log_info "$ns: registering (will take 1–3 min)..."
    fi
  done
  if [[ "$DRY_RUN" == false ]]; then
    log_info "Waiting for all providers to reach Registered state..."
    for ns in "${REQUIRED_PROVIDERS[@]}"; do
      local state="Unknown"
      while [[ "$state" != "Registered" ]]; do
        state=$(az provider show --namespace "$ns" --query registrationState -o tsv 2>/dev/null || echo "Unknown")
        [[ "$state" == "Registered" ]] && break
        echo -n "." && sleep 5
      done
      log_ok "$ns Registered"
    done
  fi

  # Create tfstate backend resources
  log_info "Creating tfstate backend (idempotent)..."
  if ! az group show --name "$TF_STATE_RG" &>/dev/null; then
    run_cmd az group create --name "$TF_STATE_RG" --location canadacentral
    log_ok "Created $TF_STATE_RG"
  else
    log_ok "$TF_STATE_RG already exists"
  fi
  if ! az storage account show --name "$TF_STATE_ACCOUNT" --resource-group "$TF_STATE_RG" &>/dev/null; then
    run_cmd az storage account create \
      --name "$TF_STATE_ACCOUNT" --resource-group "$TF_STATE_RG" \
      --location canadacentral --sku Standard_LRS --allow-blob-public-access false
    log_ok "Created storage account $TF_STATE_ACCOUNT"
  else
    log_ok "$TF_STATE_ACCOUNT already exists"
  fi
  if ! az storage container show --name "$TF_STATE_CONTAINER" --account-name "$TF_STATE_ACCOUNT" &>/dev/null; then
    run_cmd az storage container create \
      --name "$TF_STATE_CONTAINER" --account-name "$TF_STATE_ACCOUNT"
    log_ok "Created blob container $TF_STATE_CONTAINER"
  else
    log_ok "$TF_STATE_CONTAINER already exists"
  fi

  # Enable blob versioning (required for 'rollback --infra')
  local versioning_enabled
  versioning_enabled=$(az storage account blob-service-properties show \
    --account-name "$TF_STATE_ACCOUNT" \
    --query 'isVersioningEnabled' -o tsv 2>/dev/null || echo "false")
  if [[ "$versioning_enabled" != "true" ]]; then
    run_cmd az storage account blob-service-properties update \
      --account-name "$TF_STATE_ACCOUNT" --enable-versioning true
    log_ok "Enabled blob versioning on $TF_STATE_ACCOUNT (required for rollback --infra)"
  else
    log_ok "Blob versioning already enabled"
  fi

  # Create Entra app registration + service principal
  local app_id sp_obj_id
  app_id=$(az ad app list --display-name "$ENTRA_APP_NAME" --query "[0].appId" -o tsv 2>/dev/null || echo "")
  if [[ -z "$app_id" || "$app_id" == "null" ]]; then
    app_id=$(run_cmd az ad app create --display-name "$ENTRA_APP_NAME" --query appId -o tsv)
    log_ok "Created Entra app $ENTRA_APP_NAME ($app_id)"
  else
    log_ok "$ENTRA_APP_NAME already exists ($app_id)"
  fi
  sp_obj_id=$(az ad sp show --id "$app_id" --query id -o tsv 2>/dev/null \
    || run_cmd az ad sp create --id "$app_id" --query id -o tsv)
  log_ok "Service principal: $sp_obj_id"

  # Assign roles (skip if already assigned; guards Error 8)
  log_info "Assigning RBAC roles..."
  local scope_sub="/subscriptions/${subscription_id}"
  local scope_rg="${scope_sub}/resourceGroups/${RESOURCE_GROUP}"
  local tfstate_id
  tfstate_id=$(az storage account show \
    --name "$TF_STATE_ACCOUNT" --resource-group "$TF_STATE_RG" --query id -o tsv)

  local roles_and_scopes=(
    "Contributor:$scope_sub"
    "Key Vault Administrator:$scope_sub"
    "Storage Blob Data Contributor:$tfstate_id"
    "User Access Administrator:$scope_rg"  # required for azurerm_role_assignment management
  )
  for entry in "${roles_and_scopes[@]}"; do
    local role="${entry%%:*}" scope="${entry#*:}"
    local existing
    existing=$(az role assignment list \
      --assignee "$sp_obj_id" --role "$role" --scope "$scope" \
      --query "[0].id" -o tsv 2>/dev/null || echo "")
    if [[ -z "$existing" || "$existing" == "null" ]]; then
      run_cmd az role assignment create \
        --assignee "$sp_obj_id" --role "$role" --scope "$scope"
      log_ok "Assigned '$role'"
    else
      log_ok "'$role' already assigned"
    fi
  done

  # OIDC federated credentials for GitHub Actions
  log_info "Configuring OIDC federated credentials..."
  local creds=(
    "github-main:repo:${GITHUB_REPO}:ref:refs/heads/main"
    "github-prs:repo:${GITHUB_REPO}:pull_request"
  )
  for cred in "${creds[@]}"; do
    local name="${cred%%:*}" subject="${cred#*:}"
    local exists
    exists=$(az ad app federated-credential list --id "$app_id" \
      --query "[?name=='${name}'].name" -o tsv 2>/dev/null || echo "")
    if [[ -z "$exists" ]]; then
      run_cmd az ad app federated-credential create --id "$app_id" \
        --parameters "{\"name\":\"${name}\",\"issuer\":\"https://token.actions.githubusercontent.com\",\"subject\":\"${subject}\",\"audiences\":[\"api://AzureADTokenExchange\"]}"
      log_ok "Added federated credential: $name"
    else
      log_ok "Federated credential already exists: $name"
    fi
  done

  # Set GitHub Actions variables (if gh CLI authenticated)
  if command -v gh >/dev/null 2>&1 && gh auth status &>/dev/null 2>&1; then
    log_info "Setting GitHub Actions variables via gh CLI..."
    run_cmd gh variable set AZURE_CLIENT_ID       --body "$app_id"
    run_cmd gh variable set AZURE_TENANT_ID       --body "$tenant_id"
    run_cmd gh variable set AZURE_SUBSCRIPTION_ID --body "$subscription_id"
    run_cmd gh variable set ACR_NAME              --body "$ACR_NAME"
    log_ok "GitHub Actions variables set"
    log_warn "Set secrets manually in GitHub UI (Settings → Secrets and variables → Actions):"
    echo "    TF_VAR_POSTGRES_ADMIN_PASSWORD  — PostgreSQL admin password"
    echo "    TF_VAR_KEYCLOAK_ADMIN_PASSWORD  — Keycloak admin password"
    echo "    ROBOT_API_KEY                   — Robot Framework API key"
  else
    log_warn "gh CLI not authenticated. Set these GitHub Actions variables manually:"
    echo "  AZURE_CLIENT_ID       = $app_id"
    echo "  AZURE_TENANT_ID       = $tenant_id"
    echo "  AZURE_SUBSCRIPTION_ID = $subscription_id"
    echo "  ACR_NAME              = $ACR_NAME"
  fi

  # terraform init
  log_info "Running terraform init..."
  (cd "$TF_DIR" && run_cmd terraform init)
  log_ok "Terraform backend connected"

  echo ""
  log_ok "=== Bootstrap complete ==="
  echo "  AZURE_CLIENT_ID:       $app_id"
  echo "  AZURE_TENANT_ID:       $tenant_id"
  echo "  AZURE_SUBSCRIPTION_ID: $subscription_id"
}

# ── cmd: provision ────────────────────────────────────────────────────────────
cmd_provision() {
  require_cmd az; require_cmd docker; require_cmd terraform
  require_cmd dotnet; require_cmd jq; require_cmd curl; require_cmd git

  log_info "=== PROVISION [profile: $PROFILE] ==="
  [[ -f "$TF_DIR/envs/${PROFILE}.tfvars" ]] || die "Profile not found: terraform/envs/${PROFILE}.tfvars"

  check_az_login
  check_subscription
  check_providers
  check_secrets_tfvars
  check_failed_container_apps

  local sha
  sha=$(git -C "$REPO_ROOT" rev-parse HEAD)
  local api_image="${ACR_NAME}.azurecr.io/api/toten-api:sha-${sha}"
  local worker_image="${ACR_NAME}.azurecr.io/worker/toten-worker:sha-${sha}"
  local keycloak_image="${ACR_NAME}.azurecr.io/keycloak/toten-keycloak:latest"

  # Build and push images with explicit linux/amd64 (guards Error 5 — ARM Mac cross-compile)
  log_info "Building images (linux/amd64)..."
  run_cmd az acr login --name "$ACR_NAME"

  run_cmd docker buildx build --platform linux/amd64 \
    -t "$api_image" -f "$REPO_ROOT/docker/api/Dockerfile" "$REPO_ROOT" --push
  log_ok "API image: $api_image"

  run_cmd docker buildx build --platform linux/amd64 \
    -t "$worker_image" -f "$REPO_ROOT/docker/worker/Dockerfile" "$REPO_ROOT" --push
  log_ok "Worker image: $worker_image"

  run_cmd docker buildx build --platform linux/amd64 \
    -t "$keycloak_image" -f "$REPO_ROOT/docker/keycloak/Dockerfile" "$REPO_ROOT" --push
  log_ok "Keycloak image: $keycloak_image"

  # Update secrets.tfvars with built image URIs (prevents Error 2 — wrong image path)
  if [[ "$DRY_RUN" == false ]]; then
    write_secret "api_image"    "$api_image"
    write_secret "worker_image" "$worker_image"
    log_ok "Updated secrets.tfvars with image URIs"
  fi

  # Terraform plan
  log_info "Running terraform plan (profile: $PROFILE)..."
  (cd "$TF_DIR" && run_cmd terraform plan \
    -var-file="envs/${PROFILE}.tfvars" -var-file="envs/secrets.tfvars" \
    -out=tfplan -no-color 2>&1 | tee /tmp/toten-plan.txt)

  local adds changes destroys
  adds=$(grep -c " will be created" /tmp/toten-plan.txt 2>/dev/null || echo 0)
  changes=$(grep -c " will be updated" /tmp/toten-plan.txt 2>/dev/null || echo 0)
  destroys=$(grep -c " will be destroyed" /tmp/toten-plan.txt 2>/dev/null || echo 0)
  echo ""
  echo -e "  Plan: ${GREEN}+${adds} to add${NC}  ${YELLOW}~${changes} to change${NC}  ${RED}-${destroys} to destroy${NC}"
  echo ""

  if [[ "$DRY_RUN" == true ]]; then
    log_info "Dry-run complete. Full plan at /tmp/toten-plan.txt"
    return 0
  fi

  confirm "Apply this plan?" || die "Aborted."

  (cd "$TF_DIR" && run_cmd terraform apply tfplan)
  log_ok "Terraform apply complete"

  # Capture outputs to terraform/outputs.env
  local outputs_file="$TF_DIR/outputs.env"
  (cd "$TF_DIR" && terraform output -json \
    | jq -r 'to_entries[] | "TF_OUT_\(.key | ascii_upcase)=\(.value.value)"' \
    > "$outputs_file")
  log_ok "Outputs saved to terraform/outputs.env"
  source "$outputs_file"

  local api_fqdn="${TF_OUT_API_FQDN:-}" keycloak_fqdn="${TF_OUT_KEYCLOAK_FQDN:-}"
  local postgres_fqdn="${TF_OUT_POSTGRES_FQDN:-}"

  # EF Core migrations with temporary firewall rule (guards Error 6)
  log_info "Running EF Core migrations..."
  local my_ip
  my_ip=$(curl -s --max-time 10 https://api.ipify.org)
  local rule_name="developer-migration-$(date +%Y%m%d%H%M%S)"

  run_cmd az postgres flexible-server firewall-rule create \
    --resource-group "$RESOURCE_GROUP" \
    --name "toten-prod-postgres" \
    --rule-name "$rule_name" \
    --start-ip-address "$my_ip" \
    --end-ip-address "$my_ip"
  log_ok "Opened Postgres firewall for $my_ip"

  local pg_password
  pg_password=$(read_secret "postgres_admin_password")

  # ASPNETCORE_ENVIRONMENT=Development forces password auth path (not Entra token auth)
  ASPNETCORE_ENVIRONMENT=Development \
  ConnectionStrings__ToTenDB="Host=${postgres_fqdn};Database=ToTen;Username=pgadmin;Password=${pg_password};SslMode=Require;Trust Server Certificate=true" \
  run_cmd dotnet ef database update \
    --project "$REPO_ROOT/src/ToTen.Api" \
    --startup-project "$REPO_ROOT/src/ToTen.Api"
  log_ok "Migrations applied"

  run_cmd az postgres flexible-server firewall-rule delete \
    --resource-group "$RESOURCE_GROUP" \
    --name "toten-prod-postgres" \
    --rule-name "$rule_name" --yes
  log_ok "Removed temporary Postgres firewall rule"

  _run_smoke_tests "$api_fqdn" "$keycloak_fqdn"

  echo ""
  log_warn "=== Remaining manual steps ==="
  echo "  H.3  Add 6 Claim-to-Role mappers in Keycloak Admin UI (post-deploy-runbook.md §2)"
  echo "  H    Add production redirect URI to ToTen-api-swagger Keycloak client (§3)"
  echo "  D    Verify GitHub Actions variables/secrets are set (deployment-readiness.md §D)"
  echo "  CI   Monitor pipeline: GitHub → Actions → CI/CD"
}

# ── cmd: adjust ───────────────────────────────────────────────────────────────
cmd_adjust() {
  require_cmd az; require_cmd terraform; require_cmd jq; require_cmd curl

  log_info "=== ADJUST [target profile: $PROFILE] ==="
  [[ -f "$TF_DIR/envs/${PROFILE}.tfvars" ]] || die "Profile not found: terraform/envs/${PROFILE}.tfvars"

  check_az_login
  check_subscription
  check_providers
  check_secrets_tfvars

  # Show diff vs prod baseline
  if [[ "$PROFILE" != "prod" && -f "$TF_DIR/envs/prod.tfvars" ]]; then
    log_info "Changes vs prod profile:"
    diff "$TF_DIR/envs/prod.tfvars" "$TF_DIR/envs/${PROFILE}.tfvars" || true
    echo ""
  fi

  # Service Bus SKU guard — Azure does NOT allow in-place SKU changes (destroy + recreate)
  local current_sb_sku target_sb_sku
  current_sb_sku=$(cd "$TF_DIR" && terraform state show \
    module.service_bus.azurerm_servicebus_namespace.main 2>/dev/null \
    | grep '^\s*sku\s*=' | awk -F'"' '{print $2}' || echo "")
  target_sb_sku=$(grep "^service_bus_sku" "$TF_DIR/envs/${PROFILE}.tfvars" \
    | awk -F'"' '{print $2}' 2>/dev/null || echo "Standard")

  if [[ -n "$current_sb_sku" && "$current_sb_sku" != "$target_sb_sku" ]]; then
    echo ""
    log_warn "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    log_warn "  SERVICE BUS SKU CHANGE: $current_sb_sku → $target_sb_sku"
    log_warn "  Azure does NOT support in-place Service Bus namespace SKU changes."
    log_warn "  Terraform will DESTROY and RECREATE the namespace."
    log_warn "  All in-flight Service Bus messages will be permanently lost."
    log_warn "  Applications will be disconnected during recreate (~2–5 min)."
    log_warn "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo ""
    # This confirmation is NOT bypassed by --yes (intentionally)
    echo -en "${BOLD}${RED}Type 'confirm-destroy' to acknowledge Service Bus recreate: ${NC}"
    local sb_confirm; read -r sb_confirm
    [[ "$sb_confirm" == "confirm-destroy" ]] \
      || die "Aborted. Service Bus SKU change not confirmed."
  fi

  # ACA scale-to-zero note for free-tier
  if [[ "$PROFILE" == "free-tier" ]]; then
    echo ""
    log_warn "Free-tier profile note:"
    log_warn "  • API cold start: ~5–10s after idle period"
    log_warn "  • Keycloak cold start: ~30–60s (JVM + realm import)"
    log_warn "  • ACA scale-to-zero requires Terraform module changes (not applied by tfvars alone)"
    log_warn "    See docs/infra-free-tier-downgrade.md §2.3 for the required module edits"
    echo ""
  fi

  log_info "Running terraform plan (profile: $PROFILE)..."
  (cd "$TF_DIR" && run_cmd terraform plan \
    -var-file="envs/${PROFILE}.tfvars" -var-file="envs/secrets.tfvars" \
    -out=tfplan -no-color 2>&1 | tee /tmp/toten-adjust-plan.txt)

  local destroys
  destroys=$(grep -c " will be destroyed" /tmp/toten-adjust-plan.txt 2>/dev/null || echo 0)
  echo ""
  if [[ "$destroys" -gt 0 ]]; then
    log_warn "This plan destroys $destroys resource(s)."
  fi

  [[ "$DRY_RUN" == true ]] && { log_info "Dry-run complete."; return 0; }

  confirm "Apply adjustment?" || die "Aborted."

  (cd "$TF_DIR" && run_cmd terraform apply tfplan)
  log_ok "Adjustment applied (profile: $PROFILE)"

  _load_tf_outputs
  _run_smoke_tests "${TF_OUT_API_FQDN:-}" "${TF_OUT_KEYCLOAK_FQDN:-}"
}

# ── cmd: teardown ─────────────────────────────────────────────────────────────
cmd_teardown() {
  require_cmd az; require_cmd terraform

  log_info "=== TEARDOWN ==="

  check_az_login
  check_secrets_tfvars

  echo ""
  log_error "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  log_error "  DESTRUCTIVE: This will permanently destroy ALL resources in"
  log_error "  '$RESOURCE_GROUP', including the PostgreSQL database and data."
  log_error "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  echo ""
  # Teardown always requires the magic word — even with --yes
  echo -en "${BOLD}${RED}Type 'destroy' to continue or anything else to abort: ${NC}"
  local confirm_word; read -r confirm_word
  [[ "$confirm_word" == "destroy" ]] || die "Teardown aborted."

  log_info "Running terraform destroy..."
  (cd "$TF_DIR" && run_cmd terraform destroy \
    -var-file="envs/${PROFILE}.tfvars" -var-file="envs/secrets.tfvars" \
    -auto-approve)
  log_ok "Terraform destroy complete"

  # Optional: delete tfstate backend (destroys all state history)
  echo ""
  if confirm "Also delete tfstate resource group '$TF_STATE_RG'? (destroys all Terraform state history — irreversible)"; then
    run_cmd az group delete --name "$TF_STATE_RG" --yes --no-wait
    log_ok "Queued deletion of $TF_STATE_RG (running in background)"
  else
    log_info "Keeping $TF_STATE_RG (state history preserved)"
  fi

  # Remove any lingering developer Postgres firewall rules
  log_info "Cleaning up leftover Postgres firewall rules..."
  local rules
  rules=$(az postgres flexible-server firewall-rule list \
    --resource-group "$RESOURCE_GROUP" \
    --name "toten-prod-postgres" \
    --query "[?starts_with(name, 'developer-')].name" -o tsv 2>/dev/null || echo "")
  for rule in $rules; do
    run_cmd az postgres flexible-server firewall-rule delete \
      --resource-group "$RESOURCE_GROUP" --name "toten-prod-postgres" \
      --rule-name "$rule" --yes
    log_ok "Removed firewall rule: $rule"
  done

  echo ""
  log_warn "ACR images are NOT deleted by terraform destroy. Clean up manually if needed:"
  echo "  az acr repository delete --name $ACR_NAME --repository api/toten-api --yes"
  echo "  az acr repository delete --name $ACR_NAME --repository worker/toten-worker --yes"
  echo "  az acr repository delete --name $ACR_NAME --repository keycloak/toten-keycloak --yes"
}

# ── cmd: rollback ─────────────────────────────────────────────────────────────
cmd_rollback() {
  require_cmd az; require_cmd terraform; require_cmd jq; require_cmd git

  local do_infra=false do_app=false
  local state_version="" app_sha=""

  while [[ $# -gt 0 ]]; do
    case "$1" in
      --infra)          do_infra=true; shift ;;
      --app)            do_app=true; shift ;;
      --state-version)  state_version="$2"; shift 2 ;;
      --sha)            app_sha="$2"; shift 2 ;;
      *)                shift ;;
    esac
  done

  if [[ "$do_infra" == false && "$do_app" == false ]]; then
    die "Specify --infra, --app, or both.\nExamples:\n  rollback --infra\n  rollback --app --sha abc1234\n  rollback --infra --app"
  fi

  log_info "=== ROLLBACK [infra=$do_infra app=$do_app] ==="

  check_az_login
  check_secrets_tfvars

  # ── Infra layer: restore previous Terraform state from Azure Blob Storage ──
  if [[ "$do_infra" == true ]]; then
    log_info "Listing Terraform state versions from Azure Blob Storage..."
    log_warn "Note: blob versioning must be enabled (bootstrap step 5). If no versions appear, re-run bootstrap."

    local version_list
    version_list=$(az storage blob list \
      --container-name "$TF_STATE_CONTAINER" \
      --account-name "$TF_STATE_ACCOUNT" \
      --prefix "$TF_STATE_BLOB" \
      --include v \
      --query "sort_by([].{VersionId:versionId, Modified:properties.lastModified, Size:properties.contentLength, IsCurrent:isCurrentVersion}, &Modified)" \
      -o table 2>/dev/null || echo "")
    echo "$version_list"
    echo ""

    if [[ -z "$state_version" ]]; then
      echo -n "Enter VersionId to restore (from table above): "
      read -r state_version
    fi
    [[ -n "$state_version" ]] || die "No version selected."

    echo ""
    log_warn "This will OVERWRITE the current Terraform state with version: $state_version"
    log_warn "The current state will be permanently replaced."
    confirm "Proceed with state restore?" || die "Infra rollback aborted."

    local tmp_state
    tmp_state=$(mktemp /tmp/toten-rollback-XXXX.tfstate)
    run_cmd az storage blob download \
      --container-name "$TF_STATE_CONTAINER" \
      --account-name "$TF_STATE_ACCOUNT" \
      --name "$TF_STATE_BLOB" \
      --version-id "$state_version" \
      --file "$tmp_state"
    log_ok "Downloaded state version $state_version"

    [[ "$DRY_RUN" == true ]] && { log_info "Dry-run: would push $tmp_state to remote state"; return 0; }

    (cd "$TF_DIR" && run_cmd terraform state push "$tmp_state")
    log_ok "State restored"

    log_info "Reconciling plan with restored state (refresh-only)..."
    (cd "$TF_DIR" && run_cmd terraform apply \
      -var-file="envs/${PROFILE}.tfvars" -var-file="envs/secrets.tfvars" \
      -refresh-only -auto-approve)

    if confirm "Apply remaining drift shown above?"; then
      (cd "$TF_DIR" && run_cmd terraform apply \
        -var-file="envs/${PROFILE}.tfvars" -var-file="envs/secrets.tfvars" \
        -auto-approve)
    fi
  fi

  # ── App layer: re-pin container image to a prior commit SHA ──
  if [[ "$do_app" == true ]]; then
    log_info "Recent commits on origin/main:"
    git -C "$REPO_ROOT" log --oneline -10 origin/main
    echo ""

    if [[ -z "$app_sha" ]]; then
      echo -n "Enter commit SHA to roll back to: "
      read -r app_sha
    fi
    [[ -n "$app_sha" ]] || die "No SHA provided."

    # Verify the image exists in ACR before proceeding
    log_info "Verifying image exists in ACR for SHA ${app_sha:0:12}..."
    local found_tag
    found_tag=$(az acr repository show-tags \
      --name "$ACR_NAME" \
      --repository "api/toten-api" \
      --query "[?contains(@, 'sha-${app_sha}')]" \
      -o tsv 2>/dev/null | head -1 || echo "")
    [[ -n "$found_tag" ]] \
      || die "Image sha-${app_sha} not found in ACR. Only images built by CI on main are available."
    log_ok "Image verified: $found_tag"

    local api_image="${ACR_NAME}.azurecr.io/api/toten-api:sha-${app_sha}"
    local worker_image="${ACR_NAME}.azurecr.io/worker/toten-worker:sha-${app_sha}"

    if [[ "$DRY_RUN" == false ]]; then
      write_secret "api_image"    "$api_image"
      write_secret "worker_image" "$worker_image"
      log_ok "Updated secrets.tfvars: api_image → sha-${app_sha:0:12}"
    fi

    confirm "Apply Terraform to update ACA containers to SHA ${app_sha:0:12}?" || die "App rollback aborted."

    (cd "$TF_DIR" && run_cmd terraform apply \
      -var-file="envs/${PROFILE}.tfvars" -var-file="envs/secrets.tfvars" \
      -auto-approve)
    log_ok "Container apps updated to SHA ${app_sha:0:12}"

    _load_tf_outputs
    _run_smoke_tests "${TF_OUT_API_FQDN:-}" "${TF_OUT_KEYCLOAK_FQDN:-}"
  fi
}

# ── Usage ─────────────────────────────────────────────────────────────────────
usage() {
  cat <<EOF
${BOLD}scripts/toten.sh${NC} — ToTen infrastructure lifecycle CLI

${BOLD}Usage:${NC}
  ./scripts/toten.sh <command> [options]

${BOLD}Commands:${NC}
  bootstrap   One-time Azure + GitHub setup (run once before first provision)
  provision   Build images, terraform apply, EF migrations, smoke test
  adjust      Switch config profile and re-apply Terraform
  teardown    terraform destroy + optional resource group delete
  rollback    Restore previous infra state or re-pin container image SHA

${BOLD}Global options:${NC}
  --profile <name>   Config profile: ${BOLD}prod${NC} (default) | free-tier
  --yes              Skip confirmations (teardown still requires typing 'destroy')
  --dry-run          Show plan/actions without executing
  --help             Show this help

${BOLD}Rollback options:${NC}
  --infra              Restore a Terraform state version from Azure Blob Storage
  --app                Re-apply with a prior container image SHA
  --state-version <id> Blob version ID (skips interactive selection)
  --sha <sha>          Git commit SHA to roll back to (skips interactive selection)

${BOLD}Examples:${NC}
  ./scripts/toten.sh bootstrap
  ./scripts/toten.sh provision --profile prod
  ./scripts/toten.sh provision --profile prod --yes
  ./scripts/toten.sh adjust --profile free-tier
  ./scripts/toten.sh adjust --profile free-tier --dry-run
  ./scripts/toten.sh teardown
  ./scripts/toten.sh rollback --app --sha abc1234
  ./scripts/toten.sh rollback --infra
  ./scripts/toten.sh rollback --infra --app

${BOLD}Config profiles${NC} (terraform/envs/<profile>.tfvars):
  prod        Full SKUs — GP_Standard_D2s_v3 Postgres, Standard Service Bus
  free-tier   Minimal SKUs — B_Standard_B1ms Postgres, Basic Service Bus
              (ACA scale-to-zero requires separate module changes; see docs/infra-free-tier-downgrade.md §2.3)

${BOLD}Prerequisites:${NC}
  az, terraform, docker, dotnet, git, jq, curl
  gh (optional — used in bootstrap to set GitHub Actions variables)
EOF
}

# ── Main dispatcher ───────────────────────────────────────────────────────────
main() {
  [[ $# -eq 0 ]] && { usage; exit 0; }

  local rollback_args=()
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --profile)  PROFILE="$2"; shift 2 ;;
      --yes)      YES=true; shift ;;
      --dry-run)  DRY_RUN=true; shift ;;
      --help|-h)  usage; exit 0 ;;
      bootstrap|provision|adjust|teardown|rollback)
                  COMMAND="$1"; shift ;;
      # Capture rollback-specific flags to pass through
      --infra|--app|--state-version|--sha)
                  rollback_args+=("$1")
                  [[ "$1" == "--state-version" || "$1" == "--sha" ]] \
                    && rollback_args+=("$2") && shift
                  shift ;;
      *) log_warn "Unknown option: $1"; shift ;;
    esac
  done

  [[ -n "$COMMAND" ]] || { usage; exit 1; }

  case "$COMMAND" in
    bootstrap) cmd_bootstrap ;;
    provision) cmd_provision ;;
    adjust)    cmd_adjust ;;
    teardown)  cmd_teardown ;;
    rollback)  cmd_rollback "${rollback_args[@]+"${rollback_args[@]}"}" ;;
  esac
}

main "$@"
