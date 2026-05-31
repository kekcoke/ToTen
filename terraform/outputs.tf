output "resource_group_name" {
  value = azurerm_resource_group.main.name
}

output "aca_environment_name" {
  value = module.container_apps.environment_name
}

output "aca_managed_identity_id" {
  value = module.container_apps.managed_identity_id
}

output "aca_managed_identity_client_id" {
  value = module.container_apps.managed_identity_client_id
}

output "acr_login_server" {
  value = module.registry.login_server
}

output "keycloak_authority_url" {
  value = module.keycloak.authority_url
}

output "keycloak_fqdn" {
  value       = module.keycloak.fqdn
  description = "Keycloak Container App FQDN — used for Entra ID redirect URI (Section H.5) and IdP endpoint construction."
}

output "key_vault_uri" {
  value = module.key_vault.vault_uri
}

output "app_insights_connection_string" {
  value     = module.observability.connection_string
  sensitive = true
}

output "postgres_fqdn" {
  value = module.postgres.fqdn
}

output "api_name" {
  value = module.apps.api_name
}

output "worker_name" {
  value = module.apps.worker_name
}

output "api_fqdn" {
  value = module.apps.api_fqdn
}
