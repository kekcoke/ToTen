resource "azurerm_user_assigned_identity" "apps" {
  name                = "${var.prefix}-apps-id"
  location            = var.location
  resource_group_name = var.resource_group_name
}

resource "azurerm_container_app_environment" "main" {
  name                       = "${var.prefix}-cae"
  location                   = var.location
  resource_group_name        = var.resource_group_name
  log_analytics_workspace_id = var.log_analytics_workspace_id
}
