resource "azurerm_servicebus_namespace" "main" {
  name                = "${var.prefix}-sb"
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = var.sku
}

resource "azurerm_servicebus_queue" "items_events" {
  name         = "items-events"
  namespace_id = azurerm_servicebus_namespace.main.id
}

resource "azurerm_servicebus_queue" "api_queue" {
  name         = "ToTen-Api-Queue"
  namespace_id = azurerm_servicebus_namespace.main.id
}

resource "azurerm_servicebus_queue" "worker_queue" {
  name         = "ToTen-Worker-Queue"
  namespace_id = azurerm_servicebus_namespace.main.id
}

resource "azurerm_servicebus_namespace_authorization_rule" "send_listen" {
  name         = "SendListen"
  namespace_id = azurerm_servicebus_namespace.main.id
  listen       = true
  send         = true
  manage       = false
}
