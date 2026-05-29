output "hostname" {
  value = azurerm_signalr_service.main.hostname
}

output "connection_string" {
  value     = azurerm_signalr_service.main.primary_connection_string
  sensitive = true
}
