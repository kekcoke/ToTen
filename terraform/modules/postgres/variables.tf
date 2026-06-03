variable "prefix" {
  type = string
}

variable "location" {
  type = string
}

variable "resource_group_name" {
  type = string
}

variable "admin_password" {
  type      = string
  sensitive = true
}

variable "sku_name" {
  type    = string
  default = "B_Standard_B1ms"
}

variable "allowed_cidr_ranges" {
  type        = list(string)
  default     = []
  description = "CIDR ranges allowed through the PostgreSQL firewall. Each is expanded to a start/end IP rule."
}
