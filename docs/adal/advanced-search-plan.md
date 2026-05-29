# Implementation Plan: Marketplace Advanced Search
**Date**: 2026-05-29
**POC**: AdaL (Backend Agent)

## 1. Overview
Implement an Advanced Search endpoint for the Marketplace that supports geolocation (PostGIS), text search, and faceted filtering.

## 2. Technical Requirements
- **Geolocation**: Use `ST_DWithin` or `Distance` functions from NetTopologySuite/PostGIS to filter by radius.
- **Text Search**: Support filtering by `Name` and `Description`.
- **Faceting**: Filter by `CategoryId`, `Price` range, and `OrganizationId`.
- **Sorting**: Support sorting by `Distance` (nearest first) and `Price`.

## 3. Implementation Steps
1. Create `src/ToTen.Api/Features/Marketplace/Search` directory.
2. Define `SearchListingsRequest` and `SearchListingsResponse` DTOs.
3. Implement `SearchListingsEndpoint` using EF Core + NetTopologySuite spatial queries.
4. Update `MarketplaceEndpoints.cs` to register the search route.

## 4. Verification
- Verify distance-based filtering returns items within the correct radius.
- Verify price and category facets correctly narrow results.
