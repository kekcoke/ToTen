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

variable "swagger_client_id" {
  type    = string
  default = ""
}
