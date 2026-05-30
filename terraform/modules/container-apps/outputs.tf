output "environment_id" {
  value = azurerm_container_app_environment.main.id
}

output "environment_name" {
  value = azurerm_container_app_environment.main.name
}

output "environment_default_domain" {
  value = azurerm_container_app_environment.main.default_domain
}

output "managed_identity_id" {
  value = azurerm_user_assigned_identity.apps.id
}

output "managed_identity_principal_id" {
  value = azurerm_user_assigned_identity.apps.principal_id
}

output "managed_identity_client_id" {
  value = azurerm_user_assigned_identity.apps.client_id
}
