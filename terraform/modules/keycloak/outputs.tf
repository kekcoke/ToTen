output "fqdn" {
  value = azurerm_container_app.keycloak.ingress[0].fqdn
}

output "authority_url" {
  value = "https://${azurerm_container_app.keycloak.ingress[0].fqdn}/realms/ToTen"
}
