# Phase 1 Implementation Plan: Domain Modeling & DB Migrations
**Date**: 2026-05-28
**Status**: Planning

## 1. Security & Identity (Keycloak)
- Update `toten-realm.json`:
    - Add roles: `user`, `business_owner`, `internal_user`, `admin`, `super_admin`, `third_party`.
    - Update `demo` user with default roles.

## 2. Storage Slice (PostGIS)
- Add `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite` NuGet package.
- Entities:
    - `Location`: `Id`, `Name`, `OrganizationId`, `OwnerId`, `Coordinates` (Point), `Metadata` (JSONB).
    - `Container` (Box): `Id`, `Name`, `LocationId`, `OrganizationId`, `OwnerId`.

## 3. Core Domain Refactor
- Rename `Item` -> `InventoryItem`.
- Move `Price`, `ReleaseDate` to `Listing`.
- Add Fields: `OwnerId`, `OrganizationId`, `LocationId`, `BoxId`, `Attributes` (JSONB).

## 4. Manifest & Marketplace Slices
- `Manifest`: `Id`, `SourceLocationId`, `DestinationLocationId`, `Status` (Enum), `OrganizationId`.
- `Listing`: `Id`, `InventoryItemId`, `Price`, `ReleaseDate`, `Active`.
- `Offer`: `Id`, `ListingId`, `Amount`, `Status`.
- `Transaction`: `Id`, `BuyerId`, `SellerId`, `Amount`, `Timestamp`.

## 5. Social & Organization Slices
- `Organization`: `Id`, `Name`, `LegalName`, `TaxId`, `Type`, `Industry`, `DateCreated`, `DateDeleted`.
- `OrganizationMembership`: many-to-many (User ID <-> OrganizationId).
- `ChatThread`, `ChatMessage`, `Notification`, `NotificationPreference`.

## 6. Lineage & Ledger
- `ItemLineage`: Captures `InventoryItem` state snapshot as JSONB during `Transaction` events or location changes.

## 7. EF Core Configuration
- Enable `HasPostgresExtension("postgis")` in `ToTenContext`.
- Configure GIST indexes for `Location.Coordinates`.
- Configure JSONB mapping for `Attributes` and `Metadata`.
- Add Global Query Filters for `OwnerId` and `OrganizationId` (RBA).

## 8. Migration & Validation
- Run `dotnet ef migrations add ExpandDomain`.
- Run `dotnet ef database update`.
