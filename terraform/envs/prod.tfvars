environment         = "prod"
location            = "canadacentral"
postgres_sku        = "B_Standard_B1ms"
service_bus_sku     = "Standard"
signalr_sku         = "Free_F1"
acr_sku             = "Basic"
allowed_cidr_ranges    = []
terraform_principal_id = "e1eb01ff-d32e-4d74-9a1d-f57709d1221d"
# No browser-based web frontend exists yet (only the mobile client is planned, and CORS
# doesn't gate mobile — see docs/architecture-security-audit-2026-07-08.md §5). Populate
# this with the real origin(s), semicolon-separated, once one does.
allowed_origins = ""
