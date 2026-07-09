resource "azurerm_storage_account" "main" {
  # Storage account names: 3-24 lowercase alphanumeric only (no hyphens).
  name                     = "${replace(var.prefix, "-", "")}stor"
  resource_group_name      = var.resource_group_name
  location                 = var.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"

  # Defense-in-depth: even if application code (or a future regression) requests
  # PublicAccessType.Blob on a container, the storage account itself refuses to
  # honor it.
  allow_nested_items_to_be_public = false
}

resource "azurerm_storage_container" "blobs" {
  name                  = "blobs"
  storage_account_id    = azurerm_storage_account.main.id
  container_access_type = "private"
}
