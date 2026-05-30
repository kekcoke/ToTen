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
  default = "Standard_S1"
}

variable "aca_default_domain" {
  type        = string
  description = "Default domain of the ACA environment for CORS origin configuration."
}
