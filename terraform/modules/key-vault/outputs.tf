output "vault_uri" {
  value = azurerm_key_vault.main.vault_uri
}

output "postgres_password_secret_id" {
  value = azurerm_key_vault_secret.postgres_password.versionless_id
}

output "keycloak_password_secret_id" {
  value = azurerm_key_vault_secret.keycloak_password.versionless_id
}

output "servicebus_conn_string_secret_id" {
  value = azurerm_key_vault_secret.servicebus_conn_string.versionless_id
}

output "acr_password_secret_id" {
  value = azurerm_key_vault_secret.acr_password.versionless_id
}

output "signalr_conn_string_secret_id" {
  value = azurerm_key_vault_secret.signalr_conn_string.versionless_id
}

output "storage_conn_string_secret_id" {
  value = azurerm_key_vault_secret.storage_conn_string.versionless_id
}
