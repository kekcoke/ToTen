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
