# Cloud Infrastructure & IaC Skills

## Core Technologies & Competencies
- **Infrastructure as Code (IaC)**: Transitioning from standard `azd` (Azure Developer CLI / Bicep) scaffolding to enterprise-grade **Terraform** or Ansible for standardized, multi-cloud adaptable provisioning.
- **Azure Container Apps (ACA)**: Deploying, configuring, and scaling containerized backend APIs and headless background worker services.
- **Messaging & Storage**: Architecting resilient event-driven topologies with Azure Service Bus and managing unstructured data (like QR codes and manifest images) in Azure Blob Storage.
- **Identity Orchestration**: Containerizing and configuring Keycloak for dev environments, and establishing federated identity with Entra ID or other enterprise IdPs in production. Includes Aspire-specific patterns: session-lifetime containers must not use `WithDataVolume` (volumes outlive containers), realm filenames must case-match the `"realm"` JSON key for `DirImportProvider`, and the built-in HTTPS management health check (port 9000, self-signed cert) must be replaced with an HTTP realm endpoint check.

## Aspire Local Dev Patterns
- **Container Lifetime vs Volume Lifecycle**: `ContainerLifetime.Session` does not remove named Docker volumes on stop. If ephemeral state is required, omit `WithDataVolume()` entirely; otherwise stale data (old realm files, partial migrations) persists across sessions.
- **Keycloak Health Check Wiring**: `AddKeycloak` auto-registers a `HealthCheckAnnotation` against the HTTPS management interface (port 9000, self-signed cert). The AppHost `HttpClient` rejects this cert, permanently blocking `WaitFor(keycloak)`. Fix: enumerate `resource.Annotations.OfType<HealthCheckAnnotation>().ToList()`, call `Remove` on each, then `WithHttpHealthCheck("/realms/master")` on the HTTP endpoint.
- **Container Image Selection for arm64**: Before committing a `WithImageTag` value, verify the image has a `linux/arm64/v8` manifest. `docker pull <image>:<tag>` exits non-zero with a manifest error on Apple Silicon if absent. For PostGIS, `postgis/postgis:17-3.4` is the current arm64 ceiling (`17-3.5` has no arm64 manifest).