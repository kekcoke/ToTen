locals {
  image         = "${var.acr_login_server}/keycloak/toten-keycloak:latest"
  postgres_jdbc = "jdbc:postgresql://${var.postgres_fqdn}/keycloak"
}

resource "azurerm_container_app" "keycloak" {
  name                         = "${var.prefix}-keycloak"
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
    name  = "pg-password"
    value = var.postgres_admin_password
  }

  secret {
    name  = "kc-admin-password"
    value = var.keycloak_admin_password
  }

  template {
    min_replicas = 1
    max_replicas = 1

    container {
      name   = "keycloak"
      image  = local.image
      cpu    = 0.5
      memory = "1Gi"

      env {
        name  = "KC_HTTP_ENABLED"
        value = "true"
      }
      env {
        name  = "KC_PROXY_HEADERS"
        value = "xforwarded"
      }
      env {
        name  = "KC_HOSTNAME_STRICT"
        value = "false"
      }
      env {
        name  = "KC_DB"
        value = "postgres"
      }
      env {
        name  = "KC_DB_URL"
        value = local.postgres_jdbc
      }
      env {
        name  = "KC_DB_USERNAME"
        value = "pgadmin"
      }
      env {
        name        = "KC_DB_PASSWORD"
        secret_name = "pg-password"
      }
      env {
        name  = "KEYCLOAK_ADMIN"
        value = "admin"
      }
      env {
        name        = "KEYCLOAK_ADMIN_PASSWORD"
        secret_name = "kc-admin-password"
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
