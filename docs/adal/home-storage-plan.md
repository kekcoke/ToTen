# Implementation Plan: Home Storage Vertical Slice
**Date**: 2026-05-29
**POC**: AdaL (Backend Agent)

## 1. Overview
Implement the Home Storage vertical slice to manage physical locations and item movement within the inventory system.

## 2. Components to Implement
### Features/Storage/CreateLocation
- **Endpoint**: `POST /api/locations`
- **DTOs**: `CreateLocationRequest`, `LocationResponse`
- **Logic**: Validate coordinates (PostGIS), assign `OwnerId` from `IIdentityManager`, persist to DB.

### Features/Storage/MoveItemToLocation
- **Endpoint**: `POST /api/items/{itemId}/move`
- **DTOs**: `MoveItemRequest`
- **Logic**: 
  1. Verify item ownership.
  2. Verify destination location/box ownership.
  3. Update `InventoryItem.LocationId` or `BoxId`.
  4. Publish `ItemMovedEvent` via MassTransit.
  5. (Future) Write to `ItemLineage`.

## 3. Implementation Steps
1. Create `src/ToTen.Api/Features/Storage` directory structure.
2. Define DTOs for both features.
3. Implement `CreateLocationEndpoint`.
4. Implement `MoveItemToLocationEndpoint`.
5. Register endpoints in `StorageEndpoints.cs`.
6. Add integration tests in `tests/ToTen.Api.IntegrationTests/Storage`.

## 4. Verification
- Validate PostGIS point persistence for locations.
- Verify `ItemMovedEvent` is published to Service Bus.
