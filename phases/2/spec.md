
## Overview
This document outlines the overarching specification across all project phases. Agents must locate their specific phase/domain, review the assigned to-dos, and execute them strictly within their boundaries.

---

## Phase 2: Vertical Slices Implementation
**Assigned Agent**: Backend Developer Agent
**Objective**: Implement the application logic, routing, and asynchronous workflows for the new domains.
**To-Dos**:
- [ ] Implement **Home Storage Endpoints**: `CreateLocation`, `MoveItemToLocation`.
- [ ] Implement **Manifest Endpoints**: Generate Moving Manifest, associate boxes.
- [ ] Implement QR Code generation service for Manifest boxes (save to Azure Blob Storage).
- [ ] Implement **Marketplace Endpoints**: Create listing, submit offer.
- [ ] Implement Advanced Search endpoint supporting Geolocation (distance/radius), Text Search, and faceted filtering/sorting.
- [ ] Modify Marketplace `Transaction` logic to write immutable records to `ItemLineage` and publish `ItemTransferredEvent`.
- [ ] Define and implement Message Contracts for Azure Service Bus/MassTransit (e.g., `ItemMovedEvent`, `ManifestCreatedEvent`).
- [ ] Implement Background Worker Consumers for the newly created events.
- [x] Implement a `Communications` vertical slice with an ASP.NET Core SignalR `ChatHub`.
- [ ] Implement Worker consumers for `SendNotificationEvent` translating to SendGrid (Email) or Twilio (SMS).
- [x] Abstract Keycloak/Identity logic behind a generic `IIdentityManager` interface (Pluggable IAM).
- [ ] Implement ASP.NET Core Authorization Policies corresponding to the 6 roles.
- [ ] Implement `IAuthorizationHandler` for Resource-Based Authorization (verifying data ownership).
- [ ] Implement a Users/Roles vertical slice for Administrators to manage user permissions.
- [ ] Implement `Organizations` and `Memberships` vertical slices (Create/Update/Delete groups, Invite/Remove members).
- [ ] Update `IAuthorizationHandler` to support hierarchical access (OwnerId OR active OrganizationMembership).
