# Generic Agent Checkpoint Template (All Phases)

## Overview
Agents MUST validate all items in their assigned phase checklist before declaring their task complete and handing off to the next agent. Proof of completion (logs, test outputs, etc.) is required.

---

## Phase 1: Domain Modeling & DB Migrations (Architect Agent)
**Validation Checklist**:
- [x] EF Core models compile without syntax errors.
- [x] Navigation properties and foreign keys are correctly mapped.
- [x] `ItemLineage` schema includes foreign keys to items, owners, transactions, and a JSONB state snapshot.
- [x] Chat and Notification schemas are correctly mapped to users and transactions.
- [x] `dotnet ef migrations add` completes successfully.
- [ ] `dotnet ef database update` executes cleanly on a local/test database. (Migration ready, pending local DB connection).
- [x] No unauthorized modifications were made to API endpoint files.
- [x] Keycloak Realm export includes the 6 roles and required scopes.
- [x] EF models include `OwnerId` and `OrganizationId` where applicable.
- [x] `Organization` and `OrganizationMembership` entities are successfully mapped with a many-to-many relationship.
- [x] PostGIS extension is enabled in DbContext and spatial/GIST indexes are properly configured.
