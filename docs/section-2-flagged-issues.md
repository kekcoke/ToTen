# Section 2 Flagged Issues — Product Scoping Needed

**Date:** 2026-07-09
**Source:** `docs/architecture-security-audit-2026-07-08.md` §1 (1.8) and §2 (2.1–2.7)
**Purpose:** Everything below was scoped out of the Section 1 and Section 2 audit-fix MRs because it requires a product/design decision rather than a mechanical fix, or because the "correct" fix would materially change functionality beyond hardening. Each item lists the options considered and the question that needs an answer before a future MR can close it.

---

## 1.8 — No Keycloak client ready for a mobile app

**Status:** Open since the Section 1 MR (#19); never previously given a dedicated write-up.

**The problem:** `ToTen-api`, the only application-owned Keycloak client, has every OAuth grant disabled and a wildcard `redirectUris: ["/*"]`. No client in the realm is configured as a public client with PKCE and a pinned native redirect URI. There is currently no way for a mobile app to authenticate against this backend using a standard OIDC mobile flow.

| Option | Pros | Cons |
|---|---|---|
| **Authorization Code + PKCE**, dedicated public client, `pkce.code.challenge.method: S256`, redirect pinned to a custom scheme (e.g. `com.toten.app://auth/callback`) | Standard mobile OIDC pattern; mobile talks to Keycloak directly, no new backend infra; matches what `ToTen-api-swagger` already does for the web/Swagger flow | Client secrets/token handling live on-device; Keycloak realm config must be hardened (redirect URI allowlist, no wildcard) before this is safe |
| **BFF / token-proxy layer** — API issues its own session tokens, mobile never talks to Keycloak directly | Keeps all OIDC logic server-side; smaller attack surface on-device | New infrastructure that doesn't exist today; adds a session-management layer and a new class of token to secure/rotate |

**Decision criteria:** Does the team want OIDC logic on-device (simpler, standard, but exposes PKCE flow to the client) or fully server-side (more secure by default, but is new infrastructure that has to be built and maintained before any mobile auth screen can be written)? This blocks all mobile auth UI work per the audit's own sequencing (§6).

---

## 2.6 — Worker consumers are wired but do nothing

**Status:** Skipped in this MR — open product decision, no side-effect infrastructure exists to hook into yet.

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
