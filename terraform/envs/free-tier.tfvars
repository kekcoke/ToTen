environment         = "prod"
location            = "canadacentral"

# Postgres: Burstable B1ms (1 vCore, 2GB RAM) — ~$13-15/month vs ~$130 for GP_Standard_D2s_v3
# PostGIS, two databases (ToTen + keycloak), and password auth all work on B1ms.
# In-place resize via terraform apply causes a brief server restart (~1-2 min), no data loss.
postgres_sku        = "B_Standard_B1ms"

# Service Bus: Basic SKU — queues only, 10M ops/month included, ~$0/month at low volume.
# Viable because the app uses queues only (items-events, ToTen-Api-Queue, ToTen-Worker-Queue).
# WARNING: changing from Standard requires destroy + recreate (not in-place).
# Use: ./scripts/toten.sh adjust --profile free-tier (guards with explicit 'confirm-destroy' prompt)
service_bus_sku     = "Basic"

# SignalR: Free_F1 already — 20 concurrent connections, 20K messages/day.
signalr_sku         = "Free_F1"

# ACR: Basic is already the minimum paid tier ($5/month). No free tier exists for ACR.
acr_sku             = "Basic"

allowed_cidr_ranges    = []
terraform_principal_id = "e1eb01ff-d32e-4d74-9a1d-f57709d1221d"

# ACA scale-to-zero (further cost reduction — not applied by tfvars alone):
# Requires changes to terraform/modules/apps/main.tf and terraform/modules/keycloak/main.tf.
# See docs/infra-free-tier-downgrade.md §2.3 for the exact module edits needed.
# Cold-start implications: API ~5-10s, Keycloak ~30-60s (JVM + realm import).
