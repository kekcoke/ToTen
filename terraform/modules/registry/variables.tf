variable "prefix" {
  type = string
}

variable "location" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "sku" {
  type    = string
  default = "Standard"
}

variable "aca_managed_identity_principal_id" {
  type        = string
  description = "Principal ID of the ACA user-assigned managed identity; granted AcrPull."
}
