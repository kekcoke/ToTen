variable "prefix" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "aca_environment_id" {
  type = string
}

variable "managed_identity_id" {
  type = string
}

variable "managed_identity_client_id" {
  type = string
}

variable "acr_login_server" {
  type = string
}

variable "acr_admin_username" {
  type = string
}

variable "acr_admin_password" {
  type      = string
  sensitive = true
}

variable "api_image" {
  type        = string
  description = "Full ACR image URI for ToTen.Api (e.g. totenprodacr.azurecr.io/api/toten-api:sha-abc)."
}

variable "worker_image" {
  type        = string
  description = "Full ACR image URI for ToTen.Worker."
}

variable "key_vault_uri" {
  type = string
}

variable "keycloak_authority_url" {
  type = string
}

variable "keycloak_web_bff_redirect_uri" {
  type        = string
  default     = ""
  description = "Full callback URL for the web BFF (e.g. https://<api_fqdn>/auth/callback). Empty until the API's real deployed FQDN is known post-first-deploy — same bootstrap posture as allowed_origins. Must match the redirectUris baked into the ToTen-web-bff Keycloak client's realm entry (see docker/keycloak/Dockerfile's KEYCLOAK_WEB_BFF_REDIRECT_URI build-arg)."
}

variable "app_insights_connection_string" {
  type      = string
  sensitive = true
}

variable "servicebus_connection_string" {
  type      = string
  sensitive = true
}

variable "signalr_connection_string" {
  type      = string
  sensitive = true
}

variable "storage_connection_string" {
  type      = string
  sensitive = true
}

variable "postgres_fqdn" {
  type = string
}

variable "postgres_admin_password" {
  type      = string
  sensitive = true
}

variable "keycloak_web_bff_client_secret" {
  type      = string
  sensitive = true
}

variable "swagger_client_id" {
  type    = string
  default = ""
}

variable "allowed_origins" {
  type        = string
  default     = ""
  description = "Semicolon-separated list of allowed CORS origins for the API (see CorsExtensions.cs). Empty means no origin is allowed."
}
