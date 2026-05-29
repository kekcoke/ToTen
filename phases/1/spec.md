## Overview
This document outlines the overarching specification across all project phases. Agents must locate their specific phase/domain, review the assigned to-dos, and execute them strictly within their boundaries.

---

## Phase 1: Domain Modeling & DB Migrations
**Assigned Agent**: Architect Agent
**Objective**: Expand the base `Items` domain into a comprehensive inventory system.
**To-Dos**:
- [x] Refactor `Item` to a base `InventoryItem` entity.
- [x] Create EF Core models for the new **Storage Slice**: `Location`, `Container`/`Box`.
- [x] Create EF Core models for the **Manifest Slice**: `Manifest` (Source, Destination, Status).
- [x] Create EF Core models for the **Marketplace Slice**: `Listing`, `Offer`, `Transaction`.
- [x] Establish foreign key relationships (e.g., `InventoryItem` -> `LocationId`, `BoxId`).
- [x] Introduce an `ItemLineage` (or `ItemLedger`) entity to track ownership and condition history.
- [x] Implement dynamic JSONB schema support for extensible attributes.
- [x] Enable PostGIS extension, add NetTopologySuite, add spatial Point Coordinates to Location, and configure GIST/GIN indexes.
- [x] Add `OwnerId` and `OrganizationId` to `InventoryItem` and `Location` to support Resource-Based Authorization.
- [x] Update Keycloak Realm configuration (`ToTen-realm.json`) to define the 6-tier role model (`user`, `business_owner`, `internal_user`, `admin`, `super_admin`, `third_party`).
- [x] Create models for `ChatThread`, `ChatMessage`, `Notification`, and `NotificationPreference`.
- [x] Create EF Core models for `Organization` (Household/Business) and `OrganizationMembership` (many-to-many join with User).
- [x] Generate the initial Entity Framework Core migration (`dotnet ef migrations add ExpandDomain`).
