output "api_name" {
  value = azurerm_container_app.api.name
}

output "worker_name" {
  value = azurerm_container_app.worker.name
}

output "api_fqdn" {
  value = azurerm_container_app.api.latest_revision_fqdn
}
