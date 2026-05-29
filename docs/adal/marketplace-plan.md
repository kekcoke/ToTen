# Implementation Plan: Marketplace Vertical Slice
**Date**: 2026-05-29
**POC**: AdaL (Backend Agent)

## 1. Overview
Implement the Marketplace vertical slice for creating item listings, submitting offers, and processing transactions with immutable lineage records.

## 2. Components to Implement
### Features/Marketplace/CreateListing
- **Endpoint**: `POST /api/listings`
- **DTOs**: `CreateListingRequest`, `ListingResponse`
- **Logic**: Verify item ownership, create listing, publish `ItemListingEvent`.

### Features/Marketplace/SubmitOffer
- **Endpoint**: `POST /api/listings/{listingId}/offers`
- **DTOs**: `SubmitOfferRequest`, `OfferResponse`
- **Logic**: Validate listing status, record offer, notify owner (future).

### Features/Marketplace/AcceptOffer (Transaction)
- **Endpoint**: `POST /api/offers/{offerId}/accept`
- **Logic**: 
  1. Create immutable record in `ItemLineage`.
  2. Update item owner.
  3. Publish `ItemTransferredEvent`.
  4. Close listing.

## 3. Implementation Steps
1. Create `Features/Marketplace` directory structure.
2. Define DTOs for Listing and Offer flows.
3. Implement `CreateListingEndpoint`.
4. Implement `SubmitOfferEndpoint`.
5. Register endpoints in `MarketplaceEndpoints.cs`.

## 4. Verification
- Verify `ItemLineage` entry creation on transaction.
- Verify event publishing via MassTransit.
