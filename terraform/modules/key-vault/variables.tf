variable "prefix" {
  type = string
}

variable "location" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "aca_managed_identity_principal_id" {
  type        = string
  description = "Principal ID granted Key Vault Secrets User at runtime."
}

variable "postgres_admin_password" {
  type      = string
  sensitive = true
}

variable "keycloak_admin_password" {
  type      = string
  sensitive = true
}

variable "servicebus_connection_string" {
  type      = string
  sensitive = true
}

variable "acr_admin_password" {
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

variable "terraform_principal_id" {
  type        = string
  description = "Object ID granted Key Vault Secrets Officer during apply."
}
