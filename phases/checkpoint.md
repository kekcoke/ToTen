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
- [ ] `Organization` and `OrganizationMembership` entities are successfully mapped with a many-to-many relationship.
- [ ] PostGIS extension is enabled in DbContext and spatial/GIST indexes are properly configured.
