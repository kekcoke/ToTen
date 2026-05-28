# Generic Agent Checkpoint Template (All Phases)

## Overview
Agents MUST validate all items in their assigned phase checklist before declaring their task complete and handing off to the next agent. Proof of completion (logs, test outputs, etc.) is required.

---

## Phase 1: Domain Modeling & DB Migrations (Architect Agent)
**Validation Checklist**:
- [ ] EF Core models compile without syntax errors.
- [ ] Navigation properties and foreign keys are correctly mapped.
- [ ] `ItemLineage` schema includes foreign keys to items, owners, transactions, and a JSONB state snapshot.
- [ ] Chat and Notification schemas are correctly mapped to users and transactions.
- [ ] `dotnet ef migrations add` completes successfully.
- [ ] `dotnet ef database update` executes cleanly on a local/test database.
- [ ] No unauthorized modifications were made to API endpoint files.
- [ ] Keycloak Realm export includes the 6 roles and required scopes.
- [ ] EF models include `OwnerId` and `OrganizationId` where applicable.
- [ ] PostGIS extension is enabled in DbContext and spatial/GIST indexes are properly configured.

---

## Phase 2: Vertical Slices Implementation (Backend Agent)
**Validation Checklist**:
- [ ] Project builds successfully (`dotnet build`).
- [ ] Models, data transfer objects (DTOs) adhere to database schema.
- [ ] Completing a Marketplace transaction successfully creates an immutable `ItemLineage` ledger entry.
- [ ] Unit tests for new Handlers/Endpoints are written and passing.
- [ ] API runs locally via Aspire (`dotnet run --project src/ToTen.AppHost`).
- [ ] Swagger UI successfully loads and displays the newly added endpoints.
- [ ] Marketplace search correctly filters items within a specified geographic radius and sorts by distance.
- [ ] SignalR `ChatHub` accepts WebSocket connections and successfully broadcasts messages.
- [ ] Worker successfully processes a mocked `SendNotificationEvent`.
- [ ] Pub/Sub events are confirmed to be publishing to the local emulator/queue.
- [ ] Authentication works via the generic `IIdentityManager` without hard dependencies on Keycloak-specific libraries in the core API.
- [ ] API endpoints correctly enforce `[Authorize]` role policies.
- [ ] Resource authorization correctly blocks cross-user data access (e.g., User A accessing User B's inventory).

---

## Phase 3: Infrastructure as Code (Architect / DevSecOps Agent)
**Validation Checklist**:
- [ ] `terraform fmt` has been run and formatting is correct.
- [ ] `terraform validate` reports no syntax or configuration errors.
- [ ] `terraform plan` executes successfully and generates the expected infrastructure delta without state lock errors.
- [ ] Environment variables and secrets are correctly abstracted (no hardcoded credentials).
- [ ] PostgreSQL Terraform module explicitly enables PostGIS.
- [ ] Azure SignalR Service and communication provider secrets are present in the Terraform plan.

---

## Phase 4: DevSecOps & CI/CD Integration (DevSecOps Agent)
**Validation Checklist**:
- [ ] YAML syntax is valid and passes CI linting.
- [ ] Security scanners (SAST/DAST) run without breaking the build unexpectedly (unless critical vulnerabilities are found).
- [ ] The pipeline successfully builds the Docker images and pushes to the configured registry.
- [ ] Infrastructure deployment phase completes successfully in a staging/ephemeral environment.
- [ ] Pipeline successfully packs `ToTen.Shared` and `ToTen.Contracts` and publishes the `.nupkg` files to the designated artifact feed.

---

## Phase 5: Quality Automation Pipeline (QA Engineer Agent)
**Validation Checklist**:
- [ ] Ephemeral PostGIS `Testcontainers` spin up and tear down successfully during the test run; no orphan resources.
- [ ] Integration test suite passes with 100% success rate on the new Vertical Slices; no orphan resources.
- [ ] Robot Framework executes successfully against the local or staging URL.
- [ ] SignalR WebSocket connections pass automated integration testing.
- [ ] JMeter test runs without catastrophic failure and produces a baseline report.
- [ ] Test results are successfully published as pipeline artifacts.
- [ ] Integration tests successfully assert 401/403 HTTP status codes for unauthorized role and resource access.

---

## Phase 6: Advanced Capabilities & Edge Routing (Backend / Architect Agent)
**Validation Checklist**:
- [ ] YARP Gateway project compiles, runs, and successfully reverse-proxies requests to the underlying API.
- [ ] Gateway enforces configured CORS and Rate Limiting policies (verified via curl/Postman).
- [ ] Worker Service initializes Microsoft Semantic Kernel without dependency resolution errors.
- [ ] The pilot AI workflow can successfully process a mock payload and flag an item for "Human Review".
- [ ] The Human Review endpoint correctly transitions the item state in the database upon approval/rejection.
- [ ] AI workflow successfully analyzes mock lineage data and produces correct categorization/flags.