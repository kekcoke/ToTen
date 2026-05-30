terraform {
  required_version = ">= 1.5.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
  }

  # Bootstrap (run once before terraform init):
  #   az group create -n toten-tfstate-rg -l eastus
  #   az storage account create -n totentfstate -g toten-tfstate-rg -l eastus --sku Standard_LRS
  #   az storage container create -n tfstate-prod --account-name totentfstate
  backend "azurerm" {
    resource_group_name  = "toten-tfstate-rg"
    storage_account_name = "totentfstate"
    container_name       = "tfstate-prod"
    key                  = "toten.prod.terraform.tfstate"
  }
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy    = false
      recover_soft_deleted_key_vaults = true
    }
  }
}

resource "azurerm_resource_group" "main" {
  name     = local.resource_group_name
  location = var.location
}

# --- Observability (dependency root: Log Analytics feeds ACA environment and App Insights) ---

module "observability" {
  source              = "./modules/observability"
  prefix              = local.prefix
  location            = var.location
  resource_group_name = azurerm_resource_group.main.name
}

# --- Container Apps Environment + shared managed identity ---

module "container_apps" {
  source                     = "./modules/container-apps"
  prefix                     = local.prefix
  location                   = var.location
  resource_group_name        = azurerm_resource_group.main.name
  log_analytics_workspace_id = module.observability.log_analytics_workspace_id
}

# --- Data stores (independent; no cross-module deps) ---

module "postgres" {
  source              = "./modules/postgres"
  prefix              = local.prefix
  location            = var.location
  resource_group_name = azurerm_resource_group.main.name
  admin_password      = var.postgres_admin_password
  sku_name            = var.postgres_sku
  allowed_cidr_ranges = var.allowed_cidr_ranges
}

module "service_bus" {
  source              = "./modules/service-bus"
  prefix              = local.prefix
  location            = var.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = var.service_bus_sku
}

module "storage" {
  source              = "./modules/storage"
  prefix              = local.prefix
  location            = var.location
  resource_group_name = azurerm_resource_group.main.name
}

# --- Registry (depends on ACA managed identity for AcrPull assignment) ---

module "registry" {
  source                            = "./modules/registry"
  prefix                            = local.prefix
  location                          = var.location
  resource_group_name               = azurerm_resource_group.main.name
  sku                               = var.acr_sku
  aca_managed_identity_principal_id = module.container_apps.managed_identity_principal_id
}

# --- SignalR (depends on ACA environment default domain for CORS) ---

module "signalr" {
  source              = "./modules/signalr"
  prefix              = local.prefix
  location            = var.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = var.signalr_sku
  aca_default_domain  = module.container_apps.environment_default_domain
}

# --- Key Vault (depends on all modules that produce connection strings / credentials) ---

module "key_vault" {
  source                            = "./modules/key-vault"
  prefix                            = local.prefix
  location                          = var.location
  resource_group_name               = azurerm_resource_group.main.name
  aca_managed_identity_principal_id = module.container_apps.managed_identity_principal_id
  postgres_admin_password           = var.postgres_admin_password
  keycloak_admin_password           = var.keycloak_admin_password
  servicebus_connection_string      = module.service_bus.connection_string
  acr_admin_password                = module.registry.admin_password
  signalr_connection_string         = module.signalr.connection_string
  storage_connection_string         = module.storage.primary_connection_string
}

# --- Keycloak (depends on ACA env, ACR, Postgres, managed identity) ---

module "keycloak" {
  source                  = "./modules/keycloak"
  prefix                  = local.prefix
  resource_group_name     = azurerm_resource_group.main.name
  aca_environment_id      = module.container_apps.environment_id
  managed_identity_id     = module.container_apps.managed_identity_id
  acr_login_server        = module.registry.login_server
  acr_admin_username      = module.registry.admin_username
  acr_admin_password      = module.registry.admin_password
  postgres_fqdn           = module.postgres.fqdn
  postgres_admin_password = var.postgres_admin_password
  keycloak_admin_password = var.keycloak_admin_password
}

# --- Application services (Api + Worker — depends on all other modules) ---

module "apps" {
  source                         = "./modules/apps"
  prefix                         = local.prefix
  resource_group_name            = azurerm_resource_group.main.name
  aca_environment_id             = module.container_apps.environment_id
  managed_identity_id            = module.container_apps.managed_identity_id
  managed_identity_client_id     = module.container_apps.managed_identity_client_id
  acr_login_server               = module.registry.login_server
  acr_admin_username             = module.registry.admin_username
  acr_admin_password             = module.registry.admin_password
  api_image                      = var.api_image
  worker_image                   = var.worker_image
  key_vault_uri                  = module.key_vault.vault_uri
  keycloak_authority_url         = module.keycloak.authority_url
  app_insights_connection_string = module.observability.connection_string
  servicebus_connection_string   = module.service_bus.connection_string
  signalr_connection_string      = module.signalr.connection_string
  storage_connection_string      = module.storage.primary_connection_string
  postgres_fqdn                  = module.postgres.fqdn
  postgres_admin_password        = var.postgres_admin_password
}
