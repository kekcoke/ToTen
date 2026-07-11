locals {
  postgres_conn = "Host=${var.postgres_fqdn};Database=ToTen;Username=pgadmin;Password=${var.postgres_admin_password};SSL Mode=Require;Trust Server Certificate=true"
}

resource "azurerm_container_app" "api" {
  name                         = "${var.prefix}-api"
  container_app_environment_id = var.aca_environment_id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"

  identity {
    type         = "UserAssigned"
    identity_ids = [var.managed_identity_id]
  }

  registry {
    server               = var.acr_login_server
    username             = var.acr_admin_username
    password_secret_name = "acr-password"
  }

  secret {
    name  = "acr-password"
    value = var.acr_admin_password
  }

  secret {
    name  = "postgres-conn"
    value = local.postgres_conn
  }

  secret {
    name  = "servicebus-conn"
    value = var.servicebus_connection_string
  }

  secret {
    name  = "storage-conn"
    value = var.storage_connection_string
  }

  secret {
    name  = "signalr-conn"
    value = var.signalr_connection_string
  }

  secret {
    name  = "appinsights-conn"
    value = var.app_insights_connection_string
  }

  secret {
    name  = "web-bff-client-secret"
    value = var.keycloak_web_bff_client_secret
  }

  template {
    min_replicas = 0
    max_replicas = 3

    container {
      name   = "api"
      image  = var.api_image
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "AZURE_CLIENT_ID"
        value = var.managed_identity_client_id
      }
      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }
      env {
        name  = "Auth__Authority"
        value = var.keycloak_authority_url
      }
      env {
        name  = "Auth__WebBff__ClientId"
        value = "ToTen-web-bff"
      }
      env {
        name        = "Auth__WebBff__ClientSecret"
        secret_name = "web-bff-client-secret"
      }
      env {
        name  = "Auth__WebBff__RedirectUri"
        value = var.keycloak_web_bff_redirect_uri
      }
      env {
        name  = "KeyVault__Uri"
        value = var.key_vault_uri
      }
      env {
        name  = "SWAGGERUI_CLIENTID"
        value = var.swagger_client_id
      }
      env {
        name  = "AllowedOrigins"
        value = var.allowed_origins
      }
      env {
        name        = "ConnectionStrings__ToTenDB"
        secret_name = "postgres-conn"
      }
      env {
        name        = "ConnectionStrings__servicebus"
        secret_name = "servicebus-conn"
      }
      env {
        name        = "ConnectionStrings__blobs"
        secret_name = "storage-conn"
      }
      env {
        name        = "SignalR__ConnectionString"
        secret_name = "signalr-conn"
      }
      env {
        name        = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        secret_name = "appinsights-conn"
      }

      liveness_probe {
        transport        = "HTTP"
        path             = "/health/alive"
        port             = 8081
        interval_seconds = 10
      }

      readiness_probe {
        transport        = "HTTP"
        path             = "/health/ready"
        port             = 8081
        interval_seconds = 10
      }
    }
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "http"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }
}

resource "azurerm_container_app" "worker" {
  name                         = "${var.prefix}-worker"
  container_app_environment_id = var.aca_environment_id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"

  identity {
    type         = "UserAssigned"
    identity_ids = [var.managed_identity_id]
  }

  registry {
    server               = var.acr_login_server
    username             = var.acr_admin_username
    password_secret_name = "acr-password"
  }

  secret {
    name  = "acr-password"
    value = var.acr_admin_password
  }

  secret {
    name  = "servicebus-conn"
    value = var.servicebus_connection_string
  }

  secret {
    name  = "storage-conn"
    value = var.storage_connection_string
  }

  secret {
    name  = "appinsights-conn"
    value = var.app_insights_connection_string
  }

  template {
    min_replicas = 0
    max_replicas = 1

    container {
      name   = "worker"
      image  = var.worker_image
      cpu    = 0.25
      memory = "0.5Gi"

      env {
        name  = "AZURE_CLIENT_ID"
        value = var.managed_identity_client_id
      }
      env {
        name  = "KeyVault__Uri"
        value = var.key_vault_uri
      }
      env {
        name        = "ConnectionStrings__servicebus"
        secret_name = "servicebus-conn"
      }
      env {
        name        = "ConnectionStrings__blobs"
        secret_name = "storage-conn"
      }
      env {
        name        = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        secret_name = "appinsights-conn"
      }
    }

    custom_scale_rule {
      name             = "servicebus-worker-queue"
      custom_rule_type = "azure-servicebus"
      metadata = {
        queueName    = "ToTen-Worker-Queue"
        messageCount = "5"
      }
      authentication {
        secret_name       = "servicebus-conn"
        trigger_parameter = "connection"
      }
    }
  }
}
