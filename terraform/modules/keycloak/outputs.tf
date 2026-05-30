output "fqdn" {
  value = azurerm_container_app.keycloak.latest_revision_fqdn
}

output "authority_url" {
  value = "https://${azurerm_container_app.keycloak.latest_revision_fqdn}/realms/ToTen"
}
