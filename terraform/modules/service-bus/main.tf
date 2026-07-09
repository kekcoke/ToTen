resource "azurerm_servicebus_namespace" "main" {
  name                = "${var.prefix}-servicebus"
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

# Rebus error queues (audit 3.7): named explicitly here (rather than left to Rebus's
# own auto-provisioning, which the namespace's manage=true auth rule would allow) so
# they're visible/monitorable in IaC state like every other queue.
resource "azurerm_servicebus_queue" "api_error" {
  name         = "ToTen-Api-Error"
  namespace_id = azurerm_servicebus_namespace.main.id
}

resource "azurerm_servicebus_queue" "worker_error" {
  name         = "ToTen-Worker-Error"
  namespace_id = azurerm_servicebus_namespace.main.id
}

resource "azurerm_servicebus_namespace_authorization_rule" "send_listen" {
  name         = "SendListen"
  namespace_id = azurerm_servicebus_namespace.main.id
  listen       = true
  send         = true
  manage       = true
}
