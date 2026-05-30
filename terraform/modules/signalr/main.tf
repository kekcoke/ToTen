resource "azurerm_signalr_service" "main" {
  name                = "${var.prefix}-signalr"
  location            = var.location
  resource_group_name = var.resource_group_name

  sku {
    name     = var.sku
    capacity = 1
  }

  service_mode = "Default"

  cors {
    allowed_origins = [
      "https://*.${var.aca_default_domain}",
      "http://localhost:5000",
      "https://localhost:5001"
    ]
  }

  connectivity_logs_enabled = false
  messaging_logs_enabled    = false
}
