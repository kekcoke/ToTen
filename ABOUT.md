# About

**ToTen** is a hybrid inventory management and marketplace platform designed for homes, businesses, non-profit organizations, and moving companies.

## Target Users

- **Homes (Consumers)** — Manage household inventory, swap, sell, or donate items.[^3]
- **Businesses** — Track inventory and facilitate transfers between organizations.
- **Moving Companies** — Manage logistics using inventory manifests for residential moves and high-volume, non-hazardous materials (e.g. conferences, sporting events, and other large-scale operations).[^2]
- **Non-Profit Organizations (NGOs)** — Receive, donate, and redistribute inventory.[^2]

## Core Capabilities

- Near real-time inventory tracking.[^1]
- Transfer items between homeowners, businesses, moving companies, and NGOs.[^2]
- Support multiple transfer types:
  - Donations[^3]
  - Commercial transactions[^4]
- Aggregate multiple items into a single listing.[^5]
- Aggregate items into manifests for moving and logistics.[^6]
- Decompose aggregated listings or manifests back into individual items.[^7]

## Current Implementation

- **Item Audit Trail**
  - Complete audit history for inventory items.[^8]
  - Read via `GET /items/{id}/audit`, authenticated and owner/org-scoped (not open/anonymous access).[^8]

## Planned Features

- Real-time tracking.[^9]
- **FilesService**
  - Document uploads.
  - Artifact generation.
  - Moving manifest generation.
  - Receipt generation.
  - Tax document templates.
- Payment processing.
- Donation workflow.

## Pending Integration and Reconciliation

The following components exist but are not yet fully integrated:

- Notifications and `NotificationsPreferences` tables.[^10]
- `ChatThreads` and `ChatMessages` tables.[^11]
- Mapping **Keycloak** roles to platform roles.[^12]

## Footnotes

Fact-checked against `src/ToTen.Api`, `src/ToTen.Worker`, and `src/ToTen.AppHost/setup.sql` on 2026-07-11. Full detail in `docs/section-2-flagged-issues.md`.

[^1]: **False.** No hub/websocket touches inventory state — `Features/Communications/ChatHub.cs` is chat-only. Items are plain REST CRUD plus an internal Rebus event bus (`MoveItemEndpoint.cs`), not a client push channel. Contradicts the "Real-time tracking" line under Planned Features below — this is not built yet, on either side of that split.
[^2]: **Post-MVP, not built.** `Models/Organization.cs:8` restricts `Type` to `"Household, Business"` only — no Moving Company / NGO concept exists in the schema or code. `MoveItemEndpoint.cs` only relocates an item's `LocationId`/`BoxId` within the *same* owner (`item.OwnerId != user.Id` → Forbid) — it is not a cross-owner or cross-party-type transfer. MVP is scoped to Household + Business; Moving Company/NGO support is deferred (see `docs/section-2-flagged-issues.md`).
[^3]: **Post-MVP, not built.** Zero code, DTO, enum, or endpoint references donations anywhere in the repo.
[^4]: **Verified true.** `AcceptOfferEndpoint.cs` fully wires Listing → Offer → Accept → `Transaction` row → `ItemLineage` → ownership transfer → `ItemTransferredEvent`.
[^5]: **False.** `Listings."InventoryItemId"` is a singular `NOT NULL` foreign key (`setup.sql:105-113`) — one listing maps to exactly one item; there is no join table for bundling multiple items into one listing.
[^6]: **Partially true.** Items → Boxes → Manifests is real and wired (`AssociateBoxesEndpoint.cs`, `MoveItemEndpoint.cs`, FKs in `setup.sql:212-236`), but only via one hop through `Box` — there's no direct bulk "add these items to a manifest" endpoint.
[^7]: **False.** No decompose/split/reversal endpoint exists anywhere in `Features/Marketplace`, `Features/Manifests`, or `Features/Items`.
[^8]: **Resolved as of `docs/section-2-flagged-issues.md` §2.11.** Previously false/misleading: `AuditLogEntries` was written by `ToTen.Worker`'s consumers but had zero read endpoint in `ToTen.Api`. Now built: `GET /items/{id}/audit` (`Features/Items/GetItemAuditTrail/`), paginated like `GetItems`, gated by the same ownership check as `GetItem` (403/404 apply) — genuinely authenticated and owner/org-scoped, not "open" in the anonymous-access sense the original wording implied.
[^9]: Consistent with [^1] — genuinely not built yet on either the Core Capabilities or Planned Features side; this is the accurate line.
[^10]: **Verified true.** Confirmed genuinely inert — grep for `Notifications`/`NotificationPreferences` across `ToTen.Api` returns only DbSet/migration hits, no endpoints.
[^11]: **Verified true.** `ChatHub.cs:77-80` explicitly comments that persistence to `ChatMessage`/`ChatThread` is deferred pending a sender/receiver participant model; anti-abuse controls (length/flood/org-membership) were added, but no DB write happens.
[^12]: **Stale — already resolved, not pending.** `Shared/Authentication/KeycloakClaimsTransformation.cs` now flattens `realm_access.roles` / `resource_access.<client>.roles` into `ClaimTypes.Role`, registered in `Program.cs:99` (PR #20, commit `1f8aa1e`). This bullet should be considered done.