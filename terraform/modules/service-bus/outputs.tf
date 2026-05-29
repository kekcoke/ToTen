output "namespace_name" {
  value = azurerm_servicebus_namespace.main.name
}

output "connection_string" {
  value     = azurerm_servicebus_namespace_authorization_rule.send_listen.primary_connection_string
  sensitive = true
}
