# Phase 3 — VNet + Private Endpoints Implementation Notes
**Date**: 2026-05-29
**Status**: Reference (not active; current implementation uses public endpoints with firewall)

This document records the full VNet + private endpoint design for when network isolation is required (compliance, production hardening, or when migrating from a portfolio to an enterprise deployment).

---

## Overview

The current Phase 3 implementation exposes all Azure resources on public endpoints, restricted by firewall rules (IP allowlists). The private endpoint model moves all resource communication onto a private VNet, eliminating public surface area. The only public-facing resource is the ACA environment ingress.

---

## Additional Terraform Modules Required

Add the following modules alongside the existing Phase 3 modules:

```
/terraform/modules/
├── vnet/                      # VNet + subnets
│   ├── main.tf
│   ├── variables.tf
│   └── outputs.tf
└── private-endpoints/         # Private endpoint resources per service
    ├── main.tf
    ├── variables.tf
    └── outputs.tf
```

---

## VNet Module

### Subnet layout

| Subnet | CIDR | Purpose |
|---|---|---|
| `snet-aca` | `10.0.0.0/23` | ACA infrastructure subnet (delegated) |
| `snet-postgres` | `10.0.2.0/28` | PostgreSQL Flexible Server VNet integration |
| `snet-private-endpoints` | `10.0.3.0/24` | Private endpoints for Service Bus, Storage, Key Vault, ACR, SignalR |
| `snet-runners` | `10.0.4.0/28` | Self-hosted GitHub runners (optional) |

```hcl
resource "azurerm_virtual_network" "main" {
  name                = "${local.prefix}-vnet"
  address_space       = ["10.0.0.0/16"]
  location            = var.location
  resource_group_name = var.resource_group_name
}

resource "azurerm_subnet" "aca" {
  name                 = "snet-aca"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = ["10.0.0.0/23"]
  delegation {
    name = "aca-delegation"
    service_delegation {
      name = "Microsoft.App/environments"
    }
  }
}

resource "azurerm_subnet" "postgres" {
  name                 = "snet-postgres"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = ["10.0.2.0/28"]
  delegation {
    name = "postgres-delegation"
    service_delegation {
      name    = "Microsoft.DBforPostgreSQL/flexibleServers"
      actions = ["Microsoft.Network/virtualNetworks/subnets/join/action"]
    }
  }
}

resource "azurerm_subnet" "private_endpoints" {
  name                 = "snet-private-endpoints"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = ["10.0.3.0/24"]
}
```

---

## PostgreSQL Module Changes

Switch from IP firewall rules to VNet delegation:

```hcl
# Remove: azurerm_postgresql_flexible_server_firewall_rule
# Add:
resource "azurerm_postgresql_flexible_server" "main" {
  ...
  delegated_subnet_id    = var.postgres_subnet_id
  private_dns_zone_id    = azurerm_private_dns_zone.postgres.id
  public_network_access_enabled = false
}

resource "azurerm_private_dns_zone" "postgres" {
  name                = "${local.prefix}.private.postgres.database.azure.com"
  resource_group_name = var.resource_group_name
}

resource "azurerm_private_dns_zone_virtual_network_link" "postgres" {
  name                  = "postgres-vnet-link"
  resource_group_name   = var.resource_group_name
  private_dns_zone_name = azurerm_private_dns_zone.postgres.name
  virtual_network_id    = var.vnet_id
}
```

---

## Private Endpoints Module

One `azurerm_private_endpoint` resource per service. Resources requiring private endpoints:

| Service | `subresource_names` | Private DNS zone |
|---|---|---|
| Azure Service Bus | `["namespace"]` | `privatelink.servicebus.windows.net` |
| Azure Storage (blob) | `["blob"]` | `privatelink.blob.core.windows.net` |
| Azure Key Vault | `["vault"]` | `privatelink.vaultcore.azure.net` |
| Azure Container Registry | `["registry"]` | `privatelink.azurecr.io` |
| Azure SignalR | `["signalr"]` | `privatelink.service.signalr.net` |

```hcl
resource "azurerm_private_endpoint" "service_bus" {
  name                = "${local.prefix}-sb-pe"
  location            = var.location
  resource_group_name = var.resource_group_name
  subnet_id           = var.private_endpoint_subnet_id

  private_service_connection {
    name                           = "sb-connection"
    private_connection_resource_id = var.service_bus_namespace_id
    subresource_names              = ["namespace"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "sb-dns-group"
    private_dns_zone_ids = [azurerm_private_dns_zone.service_bus.id]
  }
}
```

Repeat the pattern for Storage, Key Vault, ACR, and SignalR. Each requires a corresponding `azurerm_private_dns_zone` and `azurerm_private_dns_zone_virtual_network_link`.

---

## ACA Environment Changes

Bind the ACA environment to the VNet infrastructure subnet:

```hcl
resource "azurerm_container_app_environment" "main" {
  ...
  infrastructure_subnet_id       = var.aca_subnet_id
  internal_load_balancer_enabled = false  # true if fully internal (requires App Gateway for ingress)
}
```

---

## CI Runner Network Access

When private endpoints are enabled, the GitHub Actions runner needs network access to run `terraform apply` against private resources (Postgres extension configuration, Key Vault secret population). Options:

1. **Self-hosted runner in `snet-runners`** — runner VM in the VNet; standard for enterprise.
2. **Azure Container Instance runner** — ephemeral runner provisioned inside the VNet per pipeline run.
3. **GitHub-hosted runner + VNet injection** (GitHub Enterprise / Azure-hosted runners with VNet delegation) — simplest if available.

For a portfolio deployment, option 3 or temporarily re-enabling public access during `terraform apply` (with IP restriction to the runner's egress IP) is acceptable.

---

## Migration Path from Current Public-Endpoint Implementation

1. Add the `vnet` module to `main.tf`; apply — creates VNet and subnets with no impact on existing resources.
2. Add the `private-endpoints` module; apply — creates endpoints and DNS zones alongside existing public access.
3. Set `public_network_access_enabled = false` on PostgreSQL, Service Bus, Storage, Key Vault, ACR — disable public access once private endpoints are validated.
4. Update ACA environment to bind `infrastructure_subnet_id` (requires environment recreation; plan for downtime window or blue/green cutover).
5. Remove IP firewall rules from the `postgres` and `storage` modules.
