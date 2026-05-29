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
  type        = string
  description = "User-assigned managed identity ID for the Keycloak container app."
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

variable "postgres_fqdn" {
  type = string
}

variable "postgres_admin_password" {
  type      = string
  sensitive = true
}

variable "keycloak_admin_password" {
  type      = string
  sensitive = true
}
