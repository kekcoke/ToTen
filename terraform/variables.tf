variable "environment" {
  type        = string
  default     = "prod"
  description = "Deployment environment: prod, staging, or dev."
}

variable "location" {
  type        = string
  description = "Azure region for all resources (e.g. eastus)."
}

variable "postgres_admin_password" {
  type        = string
  sensitive   = true
  description = "PostgreSQL Flexible Server administrator password."
}

variable "keycloak_admin_password" {
  type        = string
  sensitive   = true
  description = "Keycloak administrator password."
}

variable "allowed_cidr_ranges" {
  type        = list(string)
  default     = []
  description = "CIDR ranges allowed to connect to PostgreSQL (e.g. GitHub Actions runner egress IPs)."
}

variable "postgres_sku" {
  type    = string
  default = "GP_Standard_D2s_v3"
}

variable "service_bus_sku" {
  type    = string
  default = "Standard"
}

variable "signalr_sku" {
  type    = string
  default = "Standard_S1"
}

variable "acr_sku" {
  type    = string
  default = "Standard"
}

variable "api_image" {
  type        = string
  description = "Full ACR image URI for ToTen.Api (e.g. totenprodacr.azurecr.io/api/toten-api:sha-abc)."
}

variable "worker_image" {
  type        = string
  description = "Full ACR image URI for ToTen.Worker."
}

variable "terraform_principal_id" {
  type        = string
  description = "Object ID of the principal running Terraform; granted Key Vault Secrets Officer during apply."
}
