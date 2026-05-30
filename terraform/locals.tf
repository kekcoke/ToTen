locals {
  prefix              = "toten-${var.environment}"
  resource_group_name = "${local.prefix}-rg"
}
