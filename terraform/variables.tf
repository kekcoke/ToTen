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

variable "keycloak_web_bff_client_secret" {
  type        = string
  sensitive   = true
  description = "Client secret for the ToTen-web-bff confidential client, shared between the deployed Keycloak image (baked in at build time, see docker/keycloak/Dockerfile) and ToTen.Api's Auth:WebBff:ClientSecret config."
}

variable "allowed_cidr_ranges" {
  type        = list(string)
  default     = []
  description = "CIDR ranges allowed to connect to PostgreSQL (e.g. GitHub Actions runner egress IPs)."
}

variable "postgres_sku" {
  type    = string
  default = "B_Standard_B1ms"
}

variable "service_bus_sku" {
  type    = string
  default = "Basic"
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

variable "allowed_origins" {
  type        = string
  default     = ""
  description = "Semicolon-separated list of allowed CORS origins for the API. Empty until a browser-based (non-mobile) client exists — CORS doesn't gate mobile clients (see docs/architecture-security-audit-2026-07-08.md §5)."
}

variable "keycloak_web_bff_redirect_uri" {
  type        = string
  default     = ""
  description = "Full callback URL for the web BFF (e.g. https://<api_fqdn>/auth/callback). Empty until the API's real deployed FQDN is known post-first-deploy. Must match the same value baked into the ToTen-web-bff Keycloak client via docker/keycloak/Dockerfile's KEYCLOAK_WEB_BFF_REDIRECT_URI build-arg."
}
