# Section 2 Flagged Issues — Product Scoping Needed

**Date:** 2026-07-09
**Source:** `docs/architecture-security-audit-2026-07-08.md` §1 (1.8) and §2 (2.1–2.7)
**Purpose:** Everything below was scoped out of the Section 1 and Section 2 audit-fix MRs because it requires a product/design decision rather than a mechanical fix, or because the "correct" fix would materially change functionality beyond hardening. Each item lists the options considered and the question that needs an answer before a future MR can close it.

---

## 1.8 — No Keycloak client ready for a mobile app

**Status:** Resolved. Implemented **both** options from the original decision table below, applied to the two client types that actually need them — not a choice between them. Mobile (React Native) uses Authorization Code + PKCE directly against a new public `ToTen-mobile` client (Option 1), matching RFC 8252's recommendation for installed apps with OS-backed secure storage. The web frontend uses a server-side BFF (Option 2) via a new `Features/Auth` slice in `ToTen.Api` and a new confidential `ToTen-web-bff` client — the browser never sees raw tokens, only an encrypted `HttpOnly` session cookie. `ToTen-api` itself is now `bearerOnly: true` (a resource identity, not an OIDC participant) instead of the previous every-grant-disabled/wildcard-redirect dead config. See `src/ToTen.AppHost/realms/ToTen-realm.json`, `src/ToTen.Api/Features/Auth/`, `src/ToTen.Api/Shared/Identity/IKeycloakTokenClient.cs`, and `tests/ToTen.Api.IntegrationTests/Security/{KeycloakRealmConfigurationTests,WebBffAuthFlowTests}.cs`.

**The problem (as originally scoped):** `ToTen-api`, the only application-owned Keycloak client, has every OAuth grant disabled and a wildcard `redirectUris: ["/*"]`. No client in the realm is configured as a public client with PKCE and a pinned native redirect URI. There is currently no way for a mobile app to authenticate against this backend using a standard OIDC mobile flow.

| Option | Pros | Cons |
|---|---|---|
| **Authorization Code + PKCE**, dedicated public client, `pkce.code.challenge.method: S256`, redirect pinned to a custom scheme (e.g. `com.toten.app://auth/callback`) | Standard mobile OIDC pattern; mobile talks to Keycloak directly, no new backend infra; matches what `ToTen-api-swagger` already does for the web/Swagger flow | Client secrets/token handling live on-device; Keycloak realm config must be hardened (redirect URI allowlist, no wildcard) before this is safe |
| **BFF / token-proxy layer** — API issues its own session tokens, mobile never talks to Keycloak directly | Keeps all OIDC logic server-side; smaller attack surface on-device | New infrastructure that doesn't exist today; adds a session-management layer and a new class of token to secure/rotate |

**Decision criteria (as originally scoped):** Does the team want OIDC logic on-device (simpler, standard, but exposes PKCE flow to the client) or fully server-side (more secure by default, but is new infrastructure that has to be built and maintained before any mobile auth screen can be written)? This blocks all mobile auth UI work per the audit's own sequencing (§6).

**Why both, not one:** mobile and web are different threat models, not a single client-type decision. A native app has an OS keychain, so exposing it to a PKCE flow (Option 1) is the standard, secure pattern (RFC 8252) — a BFF for mobile would be new infrastructure solving a problem mobile doesn't have. A browser has no equivalent secure storage — anything JS-readable is XSS-exfiltratable — so the BFF (Option 2) is the correct hardening for web, not an alternative to Option 1. CSRF protection (`Shared/Authorization/CsrfValidationFilter.cs`) was added as a necessary consequence of introducing cookie auth, applied scheme-conditionally so mobile's bearer path is unaffected.

---

## 2.6 — Worker consumers are wired but do nothing

**Status:** Resolved for `ItemEventsHandler`/`ManifestCreatedHandler` — implemented the **audit trail table** option from the decision table below. Worker gained its own Postgres access (`WorkerDbContext`, a Worker-owned migration history distinct from `ToTenContext`'s so the two independently-migrated DbContexts sharing the `ToTen` database don't collide) and now writes one immutable `AuditLogEntry` row per `ItemMovedEvent`/`ItemListingEvent`/`ItemTransferredEvent`/`ItemDeletedEvent`/`ManifestCreatedEvent`, in addition to the existing `LogInformation` calls. `NotificationHandler`/`SendNotificationEvent` was intentionally left untouched — it already performs real work (sends notifications) and was never part of this finding. See `src/ToTen.Worker/Data/`, `src/ToTen.Worker/Consumers/{ItemEventsConsumer,ManifestCreatedConsumer}.cs`, and `tests/ToTen.Worker.Tests/Consumers/{ItemEventsHandlerTests,ManifestCreatedHandlerTests}.cs`. This closes the "audit trail vs. search index vs. notification vs. leave as log-only" decision below in favor of audit trail, per the decision criteria — it has no dependency on unfinished work (unlike the notification option's dependency on 1.9's real `INotifier`) and is directly useful for a future item-history view. See the new flagged item at the end of this document for the FK/referential-integrity follow-up this table introduces.

**The problem:** `ItemEventsHandler` and `ManifestCreatedHandler` are genuinely triggered by real Rebus events (`ItemMovedEvent`, `ItemListingEvent`, `ItemTransferredEvent`, `ItemDeletedEvent`, `ManifestCreatedEvent`) but every handler body is a single `LogInformation` call — no DB write, no side effect. `ToTen.Worker` has zero DbContext/EF Core reference today (confirmed: no `ToTenContext` or any other data-access abstraction is available to it), so any real handler body is new infrastructure, not a bug fix.

| Option | Pros | Cons |
|---|---|---|
| **Audit trail table** — Worker writes an immutable log row per event | Directly useful for a "your item was moved/sold" history view; low-risk, append-only | Needs Worker to gain DB access (new shared Data project or its own `ToTenContext`), plus a new table/migration |
| **Search index update** — Worker updates a read model or search index | Sets up infrastructure the Marketplace search endpoint could eventually use | No search index technology has been chosen yet; largest scope of the three options |
| **Trigger a notification** — Worker publishes `SendNotificationEvent` in response | Directly closes part of audit finding 1.9 (notification pipeline is otherwise fully non-functional); reuses existing (currently unpublished) contract | Depends on 1.9's notification provider work (`INotifier` real implementation) being done first, which is explicitly out of scope here too |
| **Leave as log-only** | Zero additional work | The Rebus plumbing is real and working but delivers no value; audit finding stays open indefinitely |

**Decision criteria:** What does the mobile app's initial feature set actually need to read back? Per audit §6 sequencing, prioritize by what mobile requires (item move/listing history, manifest status) rather than closing all four handler bodies uniformly.

---

## 2.3 (partial) — ChatHub message persistence

**Status:** Anti-abuse controls (length limit, flood limit, org-membership relationship check) shipped in this MR. Persistence to `ChatMessage`/`ChatThread` is deliberately not included.

**The problem:** `ChatThread`/`ChatMessage` model group/thread chat — `ChatThread` has a `Title` and a `Messages` collection, but no participant list; `ChatMessage` has a `SenderId` but no `ReceiverId`. `ChatHub.SendMessage`, meanwhile, does a 1:1 `Clients.User(receiverId)` push. There is no existing way to resolve "sender + receiver" into a `ChatThread` without inventing new schema.

| Option | Pros | Cons |
|---|---|---|
| **Thread-per-pair** — deterministically find-or-create a `ChatThread` keyed by the sorted (senderId, receiverId) pair | No new join table; reuses the existing `Messages` collection as-is | `ChatThread.Title` has no natural value for a DM; still needs *some* way to look up "the thread between these two users" without a participants table, which likely means adding one anyway |
| **Add a `ChatParticipant` join table** (`ThreadId`, `UserId`) | Properly models both DMs and future group chat; queryable ("find my threads") | New table + migration + EF config; changes the shape `ChatHub` and any future REST surface for `Communications` (currently has none — see audit §4) both depend on |
| **Leave chat ephemeral** (push-only, no history) | Zero schema work; matches current behavior exactly, just hardened | No chat history for mobile; "what did they say" is unanswerable after the fact; `ChatMessage`/`ChatThread` tables remain unused dead schema |

**Decision criteria:** Does the product need chat history/audit trail before mobile launch, or is ephemeral push-only messaging acceptable short-term? If persistence is needed, the `ChatParticipant` join table is the more future-proof of the two persistence options since `Communications` currently has no REST surface at all (audit §4) and will likely need one eventually.

---

## 2.7 — Dead event records kept, not deleted

**Status:** `ItemCreatedEvent`, `ItemUpdatedEvent`, `ItemTransactionEvent` were moved (namespace-consolidated) but not deleted, mirroring the 1.9 decision to preserve `IEventPublisher`/`EventPublisher`/`ServiceBusExtensions`/`ServiceBusProcessorFactory`.

**The problem:** All three records have zero references anywhere in the solution outside their own declaration (confirmed via exhaustive grep). The audit's own fix direction offered two paths: wire `CreateItem`/`UpdateItem` to publish `ItemCreatedEvent`/`ItemUpdatedEvent` (the "likely intent"), or delete the unused contracts.

| Option | Pros | Cons |
|---|---|---|
| **Wire up publishing** — `CreateItem`/`UpdateItem` publish their corresponding events; a Worker handler picks them up | Closes the gap the record names imply; enables an eventual audit-trail/history feature | New publish call sites + new consumer logic = new functionality, not a cleanup; overlaps with the 2.6 "what should Worker handlers actually do" decision |
| **Delete now** | Removes genuinely dead code | `ItemTransactionEvent` in particular maps to a real audit §4 gap ("no purchase-history endpoint exists") — deleting it forecloses that option without a product decision |
| **Keep, unpublished** (current state) | No functionality change; nothing forecloses future use; consistent with the 1.9 precedent | Dead code lingers until a decision is made |

**Decision criteria:** Is there a concrete near-term need — an audit trail, or the transaction/purchase-history endpoint the CRUD matrix (audit §4) already flags as missing — that would consume these events? If yes, wire them up as part of that feature work rather than in isolation. If no, delete them in a future cleanup pass.

---

## 3.5 — DAST (ZAP) scan timing

**Status:** Documented as a detective-only control in this MR (comment added in `.github/workflows/azure-dev.yml`, `CLAUDE.md` CI/CD table updated). The staging-slot alternative below is not implemented — it's new infrastructure, not a mechanical fix.

**The problem:** `dast-scan` (`.github/workflows/azure-dev.yml`) runs `needs: [deploy, terraform]` — i.e. after `deploy` has already updated the live production Container Apps via `az containerapp update`. `fail_action: true` does fail the workflow/alert on WARN-NEW findings, but nothing downstream depends on `dast-scan`, and there's no rollback step — a ZAP failure cannot prevent or undo the release that's already serving traffic.

| Option | Pros | Cons |
|---|---|---|
| **Staging slot / traffic-split scan** — provision a second Container Apps revision or environment, deploy there first, scan it, then promote | Would make DAST an actual release gate | New infrastructure: Container Apps modules currently use `revision_mode = "Single"` (no traffic-split); `terraform/envs/` has no `staging.tfvars`; real work, not a config tweak |
| **Accept as detective-only, document it** (current state) | Zero new infrastructure; matches what the pipeline already does today | DAST findings are only ever discovered after production is already running the new code |

**Decision criteria:** Is a true pre-prod release gate worth standing up a staging environment (new Terraform module work, a second ACA revision or environment, CI changes to deploy-then-promote)? If not, the current detective-only posture is fine as-is — just keep it documented so nobody mistakes `fail_action: true` for a release gate.

---

## 3.6 — `ToTen.Contracts` NuGet versioning

**Status:** Skipped in this MR — the audit's own fix direction is conditioned on "once an external consumer... exists," and none does yet.

**The problem:** `.github/workflows/azure-dev.yml`'s `nuget-publish` job packs `ToTen.Contracts` with `PackageVersion=1.0.${{ github.run_number }}` on every merge to `main` — a monotonically increasing build number under a hardcoded `1.0`, with no semver bump logic and no changelog. This is harmless today because nothing outside this repo depends on a published version; there's no compatibility contract to break yet.

| Option | Pros | Cons |
|---|---|---|
| **Adopt semver now** (bump rules tied to contract changes, add a changelog) | Ready before it's needed | No consumer exists to validate the versioning scheme against; policy decisions (what counts as breaking? changelog format?) made in a vacuum |
| **Leave as-is until a consumer exists** (current state) | No process overhead for a package nobody depends on | `ToTen.Contracts` versions remain meaningless as a compatibility signal until this is revisited |

**Decision criteria:** Revisit when the mobile app (or any other consumer) actually takes a dependency on a published `ToTen.Contracts` version — that's the point at which a version bump can break someone, and semver starts meaning something.

---

## 3.8 — `items-events` queue has no live producer or consumer

**Status:** Resolved by extending existing precedent, not by new investigation — documented here rather than left as an unstated implication. No code or Terraform change for this finding specifically.

**The problem:** `items-events` is provisioned in `terraform/modules/service-bus/main.tf` and `src/ToTen.AppHost/AppHost.cs`, and referenced by the dead `IEventPublisher`/`EventPublisher`/`ServiceBusExtensions.AddServiceBusMessaging` path (`AddServiceBusMessaging` is never called from `Program.cs`, so this registration path is unreachable) — but nothing publishes to it or consumes from it today. The real, live event path (`DeleteItem` → `IBus.Publish(new ItemDeletedEvent(...))`, and every other publisher in the Api) goes through Rebus's `ToTen-Api-Queue`/`ToTen-Worker-Queue`, not `items-events`.

| Option | Pros | Cons |
|---|---|---|
| **Remove** the queue and the dead `IEventPublisher` path | Removes genuinely unreachable code and an unused Azure resource | Contradicts the precedent already set in §2.7/finding 1.9 (below), where the same dead code was explicitly kept, not deleted |
| **Wire it up** — route `DeleteItem` through it properly | Closes the gap the audit's fix direction names | New functionality (a second publish path for one event, or migrating `DeleteItem` off Rebus), not a cleanup; no product need identified for a second event-publishing mechanism |
| **Keep, parked** (current disposition) | Consistent with the existing precedent: `IEventPublisher`/`EventPublisher`/`ServiceBusExtensions`/`ServiceBusProcessorFactory` were already kept rather than deleted (see §2.7, and the 1.9 fix note preserving "4 unused messaging files"); `items-events` is part of that same family, not a separate decision | The queue and dead code linger until/unless that broader precedent is revisited |

**Decision criteria:** This isn't actually a new decision — it inherits the one already made for §2.7/1.9 (keep parked messaging dead code rather than deleting piecemeal or wiring up without a concrete need). Revisit `items-events` together with that dead-code family as a whole, not in isolation, if a real second-subscriber use case for Rebus ever materializes (per audit 3.7's "revisit SKU if a second subscriber to the same event type is ever needed").

---

## §4 — CRUD Completeness Matrix: missing endpoints require product scoping

**Status:** Partially resolved. Unlike §1–§3, §4 is a matrix documenting gaps, not a set of prescribed fixes — most of it is genuinely missing REST surface, not hardening.

**What was fixed in this pass:** the one mechanical gap called out in §4's "Additional structural notes" — *"Pagination exists only on `Marketplace/Search`; every other list endpoint returns the full table with no limit."* `GET /items` and `GET /categories` now accept `page`/`pageSize` query params (default 20, capped at 100) and return an `X-Total-Count` header, following the existing `Marketplace/Search` pagination pattern. This is a resource-exhaustion/response-size fix, not a new authorization decision — the global rate limiter already covers every endpoint, so no policy work was needed. Response body shapes are unchanged (still flat arrays) to avoid a breaking contract change for zero benefit.

**Also resolved (follow-up pass):** Marketplace — Offer reject/counter (see row below and its own note).

**What was skipped:** every other missing endpoint in the matrix. Building any of these means inventing a new authorization/ownership model per domain — that's new product surface, not a mechanical fix, and several already overlap decisions parked elsewhere in this document.

| Domain / gap | Overlaps with | Notes |
|---|---|---|
| Categories — no Create/Read-single/Update/Delete (list-only, anonymous) | — | Full management API doesn't exist. Closest precedent is `AdminPolicy` (used on `Users`), but whether Categories should be admin-managed at all vs. seed-only-forever is an open product question. |
| Storage — Location: create-only, no list/view/edit/delete | — | Location has no read-back path at all once created. |
| Storage — Box: fully modeled entity, zero CRUD endpoints | — | Only ever referenced by ID from Move/AssociateBoxes/GenerateQR; no ownership model defined for a Box on its own. |
| Manifests — create/mutate only, no read-back, no status transition | — | No way to ever view a manifest via the API once created. |
| ~~Marketplace — Offer: no reject/counter (enum supports it)~~ | — | **Resolved.** See note below. |
| Marketplace — Transaction / ItemLineage: write-only, no read endpoints | **§2.7** | Same "audit trail vs. no product need yet" decision already parked for the dead event records — a purchase-history/lineage endpoint would consume exactly those events. |
| ~~Organizations — no "my orgs" list, no rename/edit~~ | — | **Resolved.** See Phase 7 note below. |
| ~~Memberships — no member list, no role change post-invite~~ | — | **Resolved.** See Phase 7 note below. |
| Users — no real Keycloak Admin API integration (Create/Update/Delete are no-ops or absent, Read is hardcoded mock data) | — | Largest scope item in the matrix; not a CRUD gap so much as the feature never being built. |
| Communications — no REST surface at all, SignalR-only, no persistence | **§2.3** | Already flagged: `ChatHub` anti-abuse controls shipped, persistence (`ChatThread`/`ChatMessage`) deliberately deferred pending a participant-model decision. |
| 5 of 6 authorization policies (`UserPolicy`, `BusinessOwnerPolicy`, `InternalUserPolicy`, `SuperAdminPolicy`, `ThirdPartyPolicy`) never referenced by any endpoint | — | Wiring a policy to a specific endpoint is itself a per-endpoint security decision, not a global mechanical fix — which policy applies where has to be decided domain by domain (likely alongside the missing-endpoint work above). |
| `Organization.DateDeleted` — modeled soft-delete column, never used; all deletes are hard deletes | — | Switching hard-delete → soft-delete changes query semantics at every existing read call site (list/get queries would need a `DateDeleted == null` filter added everywhere), not a mechanical toggle. |

**Decision criteria:** Prioritize by what's blocking real usage, not by closing every cell uniformly. Memberships (already the reference-pattern domain) and Marketplace Offer reject/counter are the smallest, most self-contained gaps if a next pass wants to pick one. Everything touching Communications, Marketplace Transaction/ItemLineage, or Users should be scoped together with their existing flagged items (§2.3, §2.7) rather than in isolation.

**Marketplace — Offer reject/counter: resolved (follow-up pass).** Picked over Memberships as the smaller of the two smallest self-contained gaps because `AcceptOfferEndpoint` was the only offer-resolution path in a domain that otherwise has a fully-verified, working commercial-transaction flow (`ABOUT.md` footnote 4) — a seller had no way to decline an unwanted offer at all. Scope, per product decision: the full Accept/Reject/Counter negotiation loop (not just reject), plus a companion read endpoint, plus consistent multi-offer bookkeeping.

- `POST /api/offers/{offerId}/reject` — seller-only, `Pending` → `Rejected`.
- `POST /api/offers/{offerId}/counter` — seller-only, `Pending` → `Countered`, sets a new `Offer.CounterAmount` column (nullable `decimal(18,2)`, migration `AddOfferCounterAmount`).
- `POST /api/offers/{offerId}/counter/accept` / `.../counter/reject` — buyer-only, resolve a `Countered` offer; accept transfers ownership at `CounterAmount` (not the original `Amount`).
- `GET /api/listings/{listingId}/offers` — seller-only, paginated (`page`/`pageSize`, `X-Total-Count`), the necessary companion to reject/counter since there was previously no way to discover which offers exist on a listing.
- Accepting any offer (original or countered) now auto-rejects sibling `Pending`/`Countered` offers on the same listing, so a `Pending` offer can no longer outlive the item's ownership transfer.
- The transaction/lineage/ownership-transfer logic shared by accept and counter-accept was extracted into `Features/Marketplace/Shared/OfferAcceptance.cs` rather than duplicated.

See `src/ToTen.Api/Features/Marketplace/{RejectOffer,CounterOffer,RespondToCounterOffer,GetListingOffers,Shared}/`, `src/ToTen.Api/Models/Marketplace.cs`, migration `20260712051314_AddOfferCounterAmount`, and `tests/ToTen.Api.IntegrationTests/Marketplace/MarketplaceEndpointsTests.cs`. Memberships (member list, role change) remains open — smallest gap left if a next pass wants to pick one.

---

## 2.8 — Item domain has no cross-domain DB relationship story

**Status:** New, scoped out of the §2.6 MR — flagged here rather than left as an unstated implication.

**The problem:** §2.6's audit-trail fix gave `ToTen.Worker` its own `WorkerDbContext` and a new `AuditLogEntries` table with `ItemId`/`ManifestId` columns — but they're plain `Guid?` columns with no foreign key back to `ToTen.Api`'s `InventoryItem`/`Manifest` tables, because `ToTen.Worker` deliberately doesn't share Api's entity graph or `DbContext` (see §2.6's resolution note). This is a direct consequence of Worker and Api being separate deployable services with separate schemas/migration histories against the same physical database — not a bug in this MR, but it means Item-domain data now has a real consumer outside `ToTen.Api` with zero referential-integrity enforcement between them. The same shape of problem already exists implicitly wherever Item-domain data crosses a service or schema boundary — this is the first place it's been made concrete enough to name.

| Option | Pros | Cons |
|---|---|---|
| **Accept eventual-consistency, no FK** (current state) | Zero additional work; matches how services normally integrate across a network boundary | `AuditLogEntries.ItemId`/`ManifestId` can silently reference an Item/Manifest that's since been deleted or never existed (e.g. bad producer data); nothing catches this at write time |
| **Worker validates IDs against Api** (a synchronous call-back, or a periodic reconciliation job) | Catches bad references close to write time | New coupling between Worker and Api at runtime; adds latency/failure modes to every event handler for a table that's informational, not a ledger of record |
| **Shared read model / shared schema for Item-domain identifiers** | Would let a real FK exist, and could also serve future read endpoints (§2.7, §4's Marketplace Transaction/ItemLineage gap) | Significant new infrastructure — either a distinct "Item identifiers" table both DbContexts genuinely share, or a change in how Worker and Api relate to the same database; overlaps with whatever §2.7's "wire up dead event records" decision eventually needs |

**Decision criteria:** Does the Item domain need real FK/relationship enforcement across service boundaries before mobile launch, or is "informational, eventually-consistent, no FK" the accepted long-term posture for Worker-owned (and any future non-Api-owned) tables that reference Item-domain IDs? If a concrete need shows up — e.g. the audit trail becoming user-facing history, or §2.7's dead event records getting wired up — revisit this alongside that work rather than solving it speculatively now.

---

## 2.9 — `ABOUT.md` claimed transfer capabilities that don't exist (real-time tracking, cross-party transfer, donations)

**Status:** Resolved by documentation correction + explicit MVP scope trim (this pass). No code change.

**The problem:** A product fact-check of `ABOUT.md` against the current codebase found three "Core Capabilities" bullets with zero implementation: (1) "Near real-time inventory tracking" — no hub/websocket touches inventory, Items are plain REST CRUD (`ChatHub.cs` is chat-only, unrelated); (2) "Transfer items between homeowners, businesses, moving companies, and NGOs" — `Organization.cs:8` restricts `Type` to `"Household, Business"` only, and `MoveItemEndpoint.cs` only relocates an item within the *same* owner, never across owners or party types; (3) "Donations" as a transfer type — zero code references anywhere in the repo.

| Option | Pros | Cons |
|---|---|---|
| **Build it** — extend `Organization.Type`, add a cross-owner transfer endpoint, add a donation workflow, add a real-time push channel | Closes the gap the docs already claim | Four separate, non-trivial feature builds with no current product signal that they're needed for launch |
| **Trim MVP scope, correct the docs** (chosen) | Zero new code; `ABOUT.md` now accurately describes what ships | Moving Company/NGO/Donation/real-time-tracking vision is deferred, not delivered |

**Decision criteria (resolved):** MVP is scoped to Household + Business with commercial marketplace transactions only (see `ABOUT.md` footnotes 1–3). Moving Company and NGO account types, donation workflows, and real-time inventory push are explicit post-MVP roadmap items — revisit if/when a concrete Moving Company or NGO customer need materializes.

---

## 2.10 — Listing is single-item only; no decompose/reversal endpoint for listings or manifests

**Status:** New, flagged — not resolved. No code change in this pass.

**The problem:** `ABOUT.md` claims two capabilities the schema and API don't support: "Aggregate multiple items into a single listing" and "Decompose aggregated listings or manifests back into individual items." `Listings."InventoryItemId"` is a singular `NOT NULL` foreign key (`setup.sql:105-113`) — one listing always maps to exactly one item, with no join table for bundling. Separately, no endpoint anywhere in `Features/Marketplace`, `Features/Manifests`, or `Features/Items` reverses an aggregation (pulls an item back out of a listing, or removes a box from a manifest). Manifest aggregation itself (Items → Boxes → Manifests) *is* real and wired (`AssociateBoxesEndpoint.cs`), just one-directional.

| Option | Pros | Cons |
|---|---|---|
| **Add a `Listing` ↔ `InventoryItem` join table + bundling UI/API, plus decompose endpoints** | Closes both gaps as originally envisioned; useful for bulk resale (e.g. a box lot) | Real schema migration + new endpoints across two domains; no current product signal this is needed for MVP |
| **Leave as single-item listings, correct the docs** | Zero new work; matches the trimmed MVP's commercial-marketplace happy path, where single-item resale is the common case | "Bundle and resell a box of items" and "undo a manifest/listing" remain unsupported indefinitely |

**Decision criteria:** Is bulk/bundle resale (list a box of items as one listing) or reversible aggregation (undo a manifest or listing) a real near-term need for Household/Business users, or is single-item listing sufficient for the trimmed MVP? If no concrete need surfaces, correct `ABOUT.md` (done, see footnotes 5 and 7) and leave the schema as-is.

---

## 2.11 — Item Audit Trail has no read endpoint

**Status:** Resolved — implemented the "build it now" option from the decision table below.

**The problem:** `ABOUT.md` claimed "Item Audit Trail — Complete audit history for inventory items — Open access" as a *current* capability. `ToTen.Worker`'s consumers do write one `AuditLogEntry` row per item/manifest event (§2.6's resolution), but zero read endpoint existed anywhere in `ToTen.Api` — there was no way for any caller, authenticated or not, to read this data back. "Open access" described a read path that didn't exist, not one that was merely unauthenticated.

| Option | Pros | Cons |
|---|---|---|
| **Build `GET /items/{id}/audit` (or similar) now** (chosen) | Data already exists (Worker writes it today); directly satisfies the capability `ABOUT.md` already claims; low scope — one read endpoint against an existing table | `AuditLogEntries` has no FK back to `InventoryItems` (§2.8) — a read endpoint would need to trust `ItemId` values it can't verify referentially |
| **Leave write-only, correct the docs** | Zero new work | "Item Audit Trail" remains a write-only capability with no user-facing value until a UI need appears |

**What was built:** `GET /items/{id}/audit` (`src/ToTen.Api/Features/Items/GetItemAuditTrail/`), paginated identically to `GetItems` (`page`/`pageSize`, `X-Total-Count` header), gated by the same `ResourceOwnerRequirement`/`ResourceAuthorizationHandler` ownership check as `GetItem` — 404 if the item doesn't exist, 403 if the caller doesn't own it or share its org. `AuditLogEntry` is duplicated (not shared via `ToTen.Contracts`) as a read-only model in `src/ToTen.Api/Data/` — `Data/Configurations/AuditLogEntryConfiguration.cs` maps it with `.ToTable("AuditLogEntries", t => t.ExcludeFromMigrations())`, so Api's own `dotnet ef migrations` never tries to create/alter a table Worker already owns (verified: a scratch `dotnet ef migrations add` produced an empty `Up`/`Down` and the entity is entirely absent from `ToTenContextModelSnapshot.cs`). Test coverage: `tests/ToTen.Api.IntegrationTests/Items/GetItemAuditTrailTests.cs` (ordering, pagination, empty history, 404, 403). §2.8's no-FK posture is accepted as-is here, per that finding's own decision criteria — this endpoint doesn't need to resolve it, just read what exists.

**Known caveat (not fixed, documented):** Api and Worker each run their own EF migrations independently at Aspire startup with no ordering dependency between them (`AppHost.cs` — both `.WaitFor(ToTenDb)`, neither waits on the other). If Worker hasn't yet applied its migration when Api first serves this endpoint, the query fails with "relation does not exist" until Worker catches up. Transient startup-race, not a steady-state concern — no defensive handling added, consistent with how the rest of the codebase doesn't guard against scenarios that can't happen once both services are up.

---

## 2.12 — Notifications pipeline is wired but has no producer

**Status:** New, flagged — not resolved. Scoped as Phase 10 of the MVP Phase Roadmap below.

**The problem:** `ABOUT.md` footnote 10 confirms `Notifications`/`NotificationPreferences` are genuinely inert — grep across `ToTen.Api` for either name returns only `DbSet`/migration hits, no endpoints. This is not the same gap as §2.6: the Worker's `NotificationConsumer` already performs real work (§2.6's resolution note: "intentionally left untouched — it already performs real work (sends notifications) and was never part of this finding"). The gap is entirely on the producer side — nothing in `ToTen.Api` publishes `SendNotificationEvent` from any real user-facing flow. This has gotten more concrete since PR #27: the Marketplace offer/counter negotiation loop now has five state-changing endpoints (`SubmitOffer`, `RejectOffer`, `CounterOffer`, `RespondToCounterOffer`'s accept/reject, `AcceptOffer`) and zero way for a buyer or seller to learn their offer changed state short of polling `GET /api/listings/{listingId}/offers`.

| Option | Pros | Cons |
|---|---|---|
| **Wire Marketplace offer endpoints to publish `SendNotificationEvent`** (chosen) | Producer-side only — `NotificationConsumer` already exists and already delivers; narrowly scoped to the one concrete, already-shipped pain point | Doesn't touch other domains (Item move/listing events already have `AuditLogEntry`, not notifications — a broader "notify on every item event" pass is a separate decision) |
| Build a full `INotifier`-backed notification system across all domains | Closes 1.9 completely | Unscoped, no product signal beyond Marketplace, real provider integration work |
| Leave inert (current state) | Zero work | The negotiation loop's "you got countered" UX gap persists indefinitely |

**Decision criteria (resolved):** Scope to Marketplace offer-state-change events only — that's the concrete gap the already-shipped negotiation loop introduced, and `NotificationConsumer` needs no new work to handle it. Revisit broader notification coverage (Item/Manifest events) only if a concrete need shows up.

---

## 2.13 — No CHECK/UNIQUE constraints anywhere in the schema

**Status:** Resolved (2026-07-12) as Phase 6 of the MVP Phase Roadmap below — see that section for what shipped.

**The problem:** A grep for `CHECK` and `UNIQUE` constraints across `setup.sql` and the EF Core migrations returns empty for the entire schema — not one exists. Concretely: `Organization.Type` (`Models/Organization.cs:9`, `setup.sql:180`) is free text restricted only by a code comment (`// Household, Business`), unenforced at the DB layer, even though MVP scope (§2.9) already restricts it to exactly two values. Money fields (`Transactions`/`Listings`/`Offers` `Amount`/`Price`) have no positivity constraint — a $0 or negative offer is currently insertable. Enum-backed integer columns (`Manifests.Status` `setup.sql:131`, `Offers.Status` `setup.sql:162`) have no CHECK pinning them to a valid enum range. No UNIQUE constraint exists on `Categories.Name`, `NotificationPreferences.UserId` (a user can get two preference rows), or `Locations.Name` per organization. Separately, `setup.sql` itself is stale — it's missing `Offers.CounterAmount`, added by migration `20260712051314_AddOfferCounterAmount` but never back-ported into the ERD file; migrations are the source of truth, not `setup.sql`.

| Option | Pros | Cons |
|---|---|---|
| **Add all constraints in one migration** (chosen) | Cheap, additive, no model-shape change; closes several real data-integrity gaps in a single pass | None significant — all four families (type, money, status, uniqueness) are independent guards on existing columns |
| Add `Organization.Type` only | Narrower, smaller migration | Defers money/status/uniqueness invariants with no reason to sequence them separately |
| Leave as-is | Zero risk today since app-layer code already enforces most of these in the happy path | No defense-in-depth; bad data is one bypassed validation away |

**Decision criteria (resolved):** All four constraint families are cheap enough to bundle into one migration — no reason to sequence them separately. Also correct/regenerate `setup.sql` once this migration lands so it stops drifting from the real schema.

---

## MVP Phase Roadmap

Sequencing for everything still open across `docs/architecture-security-audit-2026-07-08.md`, this document's own §2.x/§3.x/§4 findings, `ABOUT.md`'s footnotes, and `notes/architectural_improvements.md`'s 10 strategic proposals — reconciled against current code (verified 2026-07-11, not stale doc claims) and scoped with the product owner. Phases 1–5 are the audit-fix/QA/DevSecOps work already completed per `CLAUDE.md`; this roadmap picks up at Phase 6.

### Phase 6 — Schema Hardening (Data Integrity)

**Status:** Resolved (2026-07-12).

- **§2.13** (CHECK/UNIQUE constraints): shipped via migration `20260712062605_AddSchemaHardeningConstraints` — CHECK constraint on `Organization.Type` (`'Household'`/`'Business'`), CHECK constraints on `Transactions`/`Listings`/`Offers` money fields (`> 0`, including `Offers.CounterAmount` where non-null) and on `Manifests.Status`/`Offers.Status` (valid enum range), and UNIQUE indexes on `Categories.Name`, `NotificationPreferences.UserId`, and `(OrganizationId, Name)` on `Locations`. No model-shape changes — added via `IEntityTypeConfiguration<T>.ToTable(t => t.HasCheckConstraint(...))`/`HasIndex(...).IsUnique()` in the existing `Data/Configurations/` classes (`OrganizationConfiguration`, `MarketplaceConfiguration`, `ManifestConfiguration`, `CategoryEntityConfiguration`, `StorageConfiguration`, `SocialConfiguration`).
- **`Organization.DateDeleted`**: wired up as real soft-delete, scoped narrowly as decided — `Features/Organizations/OrganizationEndpoints.cs`'s `GET`/`DELETE` handlers now treat `DateDeleted != null` as not-found, and `DELETE /api/organizations/{id}` sets the timestamp instead of removing the row. Cascading into `Memberships`/`Listings` queries stayed explicitly out of scope, as decided. Test coverage: `tests/ToTen.Api.IntegrationTests/Organizations/OrganizationsEndpointsTests.cs` (`DeleteOrganization_ByOwner_ReturnsNoContent_AndOrgSoftDeleted`, `GetOrganization_AfterDelete_ReturnsNotFound`).
- **§2.8 reaffirmed:** revisited 2026-07-11 alongside this phase — the Worker/Api FK gap posture (eventual-consistency, no FK) is unchanged. No new work.
- `setup.sql` corrected in the same pass: added the previously-missing `Offers.CounterAmount` column and all of the above CHECK/UNIQUE constraints, so it no longer drifts from the migrations.
- Full test suite (139 Api integration tests + 5 Worker tests) passes with these constraints in place; one pre-existing test (`DeleteOrganization_ByOwner_ReturnsNoContent_AndOrgRemoved`) was updated to assert soft-delete semantics instead of row removal.

### Phase 7 — §4 Core Domain CRUD (Memberships, Organizations, Manifests, Storage)

**Status:** Partially resolved — Memberships and Organizations done; Manifests, Storage remain planned.

- **Memberships — resolved.** `GET /api/organizations/{orgId}/members` (paginated member list, `page`/`pageSize`/`X-Total-Count`, any org member or admin/super_admin) and `PATCH /api/organizations/{orgId}/members/{userId}/role` (Owner-role member or admin/super_admin only; blocks demoting/removing an org's sole remaining Owner with a `400`) shipped. See `src/ToTen.Api/Features/Memberships/`, `tests/ToTen.Api.IntegrationTests/Memberships/MembershipsEndpointsTests.cs`.
- **Organizations — resolved.** `GET /api/organizations` ("my orgs", scoped to the caller's memberships, excludes soft-deleted orgs, paginated) and `PATCH /api/organizations/{id}` (rename, Owner-role member or admin/super_admin only) shipped. See `src/ToTen.Api/Features/Organizations/`, `tests/ToTen.Api.IntegrationTests/Organizations/OrganizationsEndpointsTests.cs`, `tests/ToTen.Api.IntegrationTests/Security/OrganizationAccessTests.cs`.
- **Manifests** — `GET /api/manifests/{id}` + `GET /api/manifests` (paginated, ownership-gated, following `GetItemAuditTrail`'s pattern: `page`/`pageSize`, `X-Total-Count`, 404/403 via `ResourceOwnerRequirement`) plus a status-transition endpoint (e.g. `PATCH /api/manifests/{id}/status`). Currently zero `MapGet` exists in `Features/Manifests/` — a manifest can never be viewed again once created, directly undercutting the "aggregate items into manifests for logistics" capability `ABOUT.md` already claims as built.
- **Storage** — `Box` CRUD (`Features/Storage/CreateBox/`, `GetBox/`, `UpdateBox/`, `DeleteBox/`) reusing the `ResourceOwnerRequirement` pattern already proven on Items (Box belongs to a Location which belongs to an Organization — gate by org membership the same way, no new authorization concept). `Location` gains list/get/update/delete to pair with the existing create-only endpoint.
- **Categories:** explicitly **not building** CRUD — seed-only-forever confirmed as sufficient for MVP. Correct any doc language implying otherwise; zero endpoint work.
- **Users/Keycloak Admin integration:** explicitly **deferred** past this MVP push — largest scope item in the whole matrix (real Keycloak Admin REST client, credential handling, role-sync), no concrete blocker identified for Household/Business commercial-marketplace launch specifically.
- **Authorization policies:** the 5 unwired policies (`UserPolicy`, `BusinessOwnerPolicy`, `InternalUserPolicy`, `SuperAdminPolicy`, `ThirdPartyPolicy`) are wired **opportunistically** as each endpoint above is built — no dedicated policy-wiring pass. `AdminPolicy` and `ResourceOwnerRequirement` remain the only two policies with established precedent going in.

### Phase 8 — Marketplace Transaction/Lineage Read Surface (closes §2.7 + §4 together)

**Status:** Planned.

Wire `ItemTransactionEvent` publishing into `Features/Marketplace/Shared/OfferAcceptance.cs` (the shared transaction/lineage/ownership-transfer logic used by both `AcceptOffer` and `RespondToCounterOffer`), then build a read endpoint (`GET /api/listings/{listingId}/transactions` or `GET /api/transactions`, ownership-gated, paginated like the other read endpoints in this roadmap) against the existing `Transaction`/`ItemLineage` tables. This resolves §2.7's "wire up vs. delete vs. keep parked" decision for `ItemTransactionEvent` specifically, and closes §4's Marketplace Transaction/ItemLineage read gap in the same pass, since they're the same underlying need (purchase/lineage history). `ItemCreatedEvent`/`ItemUpdatedEvent` remain explicitly parked — no concrete consumer identified for either.

**Full and Partial Refunds (Phase 8 sub-scope)**

**Status:** Planned.

**The problem:** Once a `Transaction` row exists (written by `OfferAcceptance.cs` from either `AcceptOffer` or `RespondToCounterOffer`'s counter-accept path), there is no way to reverse it. No refund concept exists anywhere in the schema, endpoints, or events — a seller has no way to refund a dissatisfied buyer, partially or fully, and a *full* refund additionally has no mechanism to reverse the ownership transfer `OfferAcceptance.cs` already performed (the item stays with the buyer even if their money comes back).

**Schema decision: new `Refund` table vs. extending `Transaction`**

| Option | Pros | Cons |
|---|---|---|
| **New `Refund` table** (chosen) — `Id`, `TransactionId` (FK), `Amount`, `Reason`, `InitiatedBy`, `Status` (`Pending`/`Completed`/`Failed`), `CreatedAt`; refunded-to-date derived as `SUM(Amount)` over `Completed` rows for a given `TransactionId` | Supports multiple partial refunds against one transaction with a per-action audit trail (who, why, when); mirrors the existing one-table-per-domain-event precedent (`Transaction`, `Offer`, `AuditLogEntry`) instead of collapsing state into a single mutable row; avoids a lost-update race when two partial refunds against the same transaction are processed concurrently | New table + migration + FK; `Transaction`'s refund status becomes a computed/query value rather than a stored column |
| Extend `Transaction` — add `RefundedAmount decimal`, `RefundStatus` enum, `RefundedAt` | No new table; single row to query | Only tracks a running total, not individual refund events (no reason, no initiator, no per-refund history); concurrent partial refunds need row-level locking or optimistic concurrency on `Transaction` to avoid lost updates; breaks the one-table-per-event precedent used everywhere else in this domain |

**Decision criteria (resolved):** Same reasoning already applied to §2.6's Worker audit trail and the Offer negotiation loop (Phase 7's predecessor work) — an append-only table per state-changing action beats a mutable running-total column, both for audit trail and for concurrency safety. `Refund` as its own table, FK'd to `Transaction`, is the chosen shape; no changes to `Transaction` itself.

**High-level implementation to-do:**

1. **Schema** — `Models/Marketplace.cs`: new `Refund` entity (`Id`, `TransactionId`, `Amount`, `Reason`, `InitiatedBy`, `Status`, `CreatedAt`) and `RefundStatus` enum (`Pending`, `Completed`, `Failed`). `MarketplaceConfiguration.cs`: new `RefundConfiguration` — FK to `Transaction`, CHECK `Amount > 0` (same pattern as Phase 6's money constraints), index on `TransactionId`. One migration; no change to `Transaction`.
2. **Shared logic** — new `Features/Marketplace/Shared/RefundProcessing.cs`, mirroring `OfferAcceptance.cs`: validates `Amount <= Transaction.Amount - SUM(existing Completed refunds)`, marks the new `Refund` row `Completed` (no external payment gateway exists in this codebase — a refund is a ledger entry, not a real money movement, consistent with how `Transaction` itself never touches a payment processor), and — **only when the refund is full** (cumulative `Completed` amount equals `Transaction.Amount`) — reverses ownership by writing a new `ItemLineage` row transferring the item back to `Transaction.SellerId`, reusing the lineage-write logic `OfferAcceptance.cs` already established. Partial refunds are financial-only; no ownership change.
3. **Endpoints** — `Features/Marketplace/RefundTransaction/`: `POST /api/transactions/{transactionId}/refunds` (seller-only; body `{ amount, reason, full }`, where `full: true` short-circuits `amount` to the remaining balance) → `201` with the new `Refund`; `400` if `amount` exceeds the remaining balance or the transaction is already fully refunded. `Features/Marketplace/GetTransactionRefunds/`: `GET /api/transactions/{transactionId}/refunds` (paginated like every other list endpoint in this roadmap — `page`/`pageSize`, `X-Total-Count`), gated by the same buyer-or-seller ownership check `GetListingOffers` already uses.
4. **Publisher** — new `RefundIssuedEvent` in `ToTen.Contracts/Events/ItemEvents.cs` (`RefundId`, `TransactionId`, `InventoryItemId`, `Amount`, `IsFullRefund`), published from `RefundProcessing.cs` once the `Refund` reaches `Completed`, following the same publish-on-state-change pattern the Offer endpoints already use.
5. **Consumer** — extend `ItemEventsConsumer` (§2.6's existing audit-trail handler) to also handle `RefundIssuedEvent` and write an `AuditLogEntry` row, reusing the infrastructure Phase 6 already built rather than standing up a new consumer. Notifying both `BuyerId` and `SellerId` via `SendNotificationEvent` depends on Phase 10 (Notifications Pipeline) landing first — either sequence this refund work after Phase 10, or ship steps 1–4/6 first and add the notification call as a small follow-up once Phase 10 exists. Flagging this dependency explicitly rather than silently reordering the roadmap.
6. **Tests** — partial refund leaves `ItemLineage`/ownership unchanged and sets `Refund.Status = Completed`; full refund (single-shot or cumulative across multiple partials) reverses `ItemLineage` back to the seller; an over-refund attempt (`amount` exceeds remaining balance) returns `400`; a non-seller caller returns `403`.

### Phase 9 — Communications Persistence (completes §2.3)

**Status:** Planned.

Add a `ChatParticipant` join table (`ThreadId`, `UserId`) and migration — the more future-proof of §2.3's two persistence options, since `Communications` has no REST surface at all today. Build the accompanying REST surface (thread list, thread history) under a new `Features/Communications/` endpoint set. Update `ChatHub.SendMessage` to resolve/find-or-create a thread through the participant model and persist each message, instead of the current push-only behavior (`ChatHub.cs:77-80`'s deferred-persistence comment gets removed once this ships). Existing anti-abuse controls (length/flood/org-membership) are unaffected.

### Phase 10 — Notifications Pipeline (§2.12)

**Status:** Planned.

Wire `SubmitOffer`, `RejectOffer`, `CounterOffer`, `RespondToCounterOffer` (both accept and reject), and `AcceptOffer` to publish `SendNotificationEvent` on each state transition. No new consumer infrastructure — `NotificationConsumer` already delivers real notifications (§2.6). Producer-side wiring only.

### Phase 11 — Dead Code / Parked Decisions (status-quo reaffirmed)

**Status:** Reaffirmed 2026-07-11, no new work.

- **§2.7** — `ItemCreatedEvent`/`ItemUpdatedEvent` remain parked; `ItemTransactionEvent` resolved via Phase 8.
- **§2.8** — no-FK, eventual-consistency posture unchanged; see Phase 6.
- **§3.5** (DAST timing), **§3.6** (NuGet versioning), **§3.8** (`items-events` queue) — still correctly parked per their own decision criteria; no external consumer, no staging environment, and no second-subscriber need have materialized.
- **§2.10** (listing bundle/decompose) — still correctly deferred; no bulk-resale or reversible-aggregation need has surfaced for Household/Business MVP scope.

### Phase 12 — Post-MVP Architectural Backlog

**Status:** Triaged 2026-07-11, from `notes/architectural_improvements.md`.

**Already substantially implemented — not backlog items:**

| # | Proposal | Evidence |
|---|---|---|
| 3 | OpenTelemetry observability | `ToTen.ServiceDefaults` already configures OTEL for ASP.NET Core/HttpClient/EF Core (per `CLAUDE.md`) |
| 5 | Multi-tier automated QA pipeline | Robot Framework + JMeter already integrated as CI jobs (`robot-tests`, `performance-test` in `azure-dev.yml`) |
| 6 | Secure-by-design DevSecOps | SAST (CodeQL) + DAST (ZAP) already run in CI (`codeql.yml`, `dast-scan` job) |
| 7 | Universal IaC standardization | `terraform/` already provisions all infrastructure via 9 modules — this is the current state, not a proposal |
| 8 | Pluggable IAM abstraction | `IIdentityManager`/`KeycloakIdentityManager` already isolates Keycloak-specific logic (§1.8's resolution) |

**Genuine post-MVP backlog** (no phase number — revisit only if a concrete driver emerges, matching this doc's existing pattern):

- **#1** Dynamic Schema Engine (EAV/JSONB) — high risk, no current multi-tenant/custom-schema need.
- **#2** Pluggable event-bus abstraction (MassTransit/Dapr) — no current multi-cloud requirement; Rebus/Azure Service Bus works today.
- **#4** AI-driven human-in-the-loop workflow engine — high risk, no current pilot workflow identified.
- **#10** YARP API gateway — no current need for edge routing beyond what CORS/rate-limiting already handle in-process.

**#9** (reusable NuGet component ecosystem) is not separately tracked here — it's the same decision as **§3.6**, which is already correctly parked pending an external consumer.
