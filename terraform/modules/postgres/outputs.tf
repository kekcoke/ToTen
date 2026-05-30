output "fqdn" {
  value = azurerm_postgresql_flexible_server.main.fqdn
}

output "server_name" {
  value = azurerm_postgresql_flexible_server.main.name
}

output "admin_login" {
  value = azurerm_postgresql_flexible_server.main.administrator_login
}
