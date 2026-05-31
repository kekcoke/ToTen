resource "azurerm_postgresql_flexible_server" "main" {
  name                          = "${var.prefix}-postgres"
  resource_group_name           = var.resource_group_name
  location                      = var.location
  version                       = "17"
  administrator_login           = "pgadmin"
  administrator_password        = var.admin_password
  storage_mb                    = 32768
  sku_name                      = var.sku_name
  backup_retention_days         = 7
  geo_redundant_backup_enabled  = false
  public_network_access_enabled = true

  lifecycle {
    ignore_changes = [zone]
  }
}

# Enable PostGIS — EF Core migration calls CREATE EXTENSION IF NOT EXISTS postgis.
resource "azurerm_postgresql_flexible_server_configuration" "postgis" {
  name      = "azure.extensions"
  server_id = azurerm_postgresql_flexible_server.main.id
  value     = "POSTGIS"
}

resource "azurerm_postgresql_flexible_server_database" "toten" {
  name      = "ToTen"
  server_id = azurerm_postgresql_flexible_server.main.id
  collation = "en_US.utf8"
  charset   = "utf8"
}

resource "azurerm_postgresql_flexible_server_database" "keycloak" {
  name      = "keycloak"
  server_id = azurerm_postgresql_flexible_server.main.id
  collation = "en_US.utf8"
  charset   = "utf8"
}

# Allow internal Azure-hosted traffic (0.0.0.0/0.0.0.0 is the Azure magic rule).
resource "azurerm_postgresql_flexible_server_firewall_rule" "azure_services" {
  name             = "allow-azure-services"
  server_id        = azurerm_postgresql_flexible_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# CI runner / operator IPs supplied via var.allowed_cidr_ranges.
resource "azurerm_postgresql_flexible_server_firewall_rule" "allowed_ranges" {
  for_each         = toset(var.allowed_cidr_ranges)
  name             = "allow-${replace(replace(each.value, "/", "-"), ".", "-")}"
  server_id        = azurerm_postgresql_flexible_server.main.id
  start_ip_address = cidrhost(each.value, 0)
  end_ip_address   = cidrhost(each.value, -1)
}
