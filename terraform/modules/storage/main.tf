resource "azurerm_storage_account" "main" {
  # Storage account names: 3-24 lowercase alphanumeric only (no hyphens).
  name                     = "${replace(var.prefix, "-", "")}stor"
  resource_group_name      = var.resource_group_name
  location                 = var.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"
}

resource "azurerm_storage_container" "blobs" {
  name                  = "blobs"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}
