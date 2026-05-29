# Implementation Plan: Authorization & Organizations
**Date**: 2026-05-29
**POC**: AdaL (Backend Agent)

## 1. Overview
Implement the final architectural components for Phase 2: Role-based policies, Resource-based authorization, and the Organization/Membership vertical slices.

## 2. Components to Implement
### Authorization Policies
- Define policies for: `user`, `business_owner`, `internal_user`, `admin`, `super_admin`, `third_party`.
- Integrate policies into the DI container.

### Resource-Based Authorization
- **Requirement**: `ResourceOwnerRequirement`.
- **Handler**: `ResourceAuthorizationHandler` checking `OwnerId` or active `OrganizationMembership`.
- Support checking access for `InventoryItem`, `Location`, and `Box`.

### Vertical Slice: Organizations
- **Endpoints**: `POST /api/organizations`, `GET /api/organizations/{id}`, `PUT /api/organizations/{id}`, `DELETE /api/organizations/{id}`.
- Logic for creating household/business groups.

### Vertical Slice: Memberships
- **Endpoints**: `POST /api/organizations/{id}/members` (Invite), `DELETE /api/organizations/{id}/members/{userId}` (Remove).

## 3. Implementation Steps
1. Define Role Constants.
2. Configure Authorization Policies in `Program.cs` or a shared extension.
3. Implement `ResourceAuthorizationHandler`.
4. Create `Features/Organizations` and `Features/Memberships` slices.
5. Register endpoints.

## 4. Verification
- Unit tests for `ResourceAuthorizationHandler` logic.
- Integration tests for cross-tenant data isolation.
