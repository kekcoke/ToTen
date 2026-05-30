# Phase 5 Implementation Plan: Quality Automation Pipeline
**Date**: 2026-05-29
**Status**: In Progress
**Agent**: QA Engineer Agent
**Branch**: `implement/phase-5`

---

## Context & Baseline

Phase 4 delivered a 6-job Terraform-native CI/CD pipeline (lint-test, docker-build-push, terraform, deploy, dast-scan, nuget-publish). The existing integration test suite covers only the Items vertical slice (5 tests) and has a broken factory — `ToTenWebApplicationFactory` required an `IDatabaseContainer` constructor parameter that xUnit v3's `IClassFixture<T>` cannot inject automatically, causing all tests to fail at host construction.

**Phase 5 objective**: Repair the test infrastructure, expand integration coverage to all 9 vertical slices, add security boundary tests (401/403 per role, cross-tenant), add SignalR hub integration tests, scaffold Robot Framework acceptance tests as a post-deploy CI job, and add a JMeter baseline load test artifact.

---

## Resolved Design Decisions

| Gap | Decision |
|---|---|
| Factory constructor injection | Self-contained `IAsyncLifetime` factory — `PostgreSqlContainer` declared inline; no external injection |
| PostGIS image | `postgis/postgis:17-3.4` via `PostgreSqlBuilder.WithImage()` — existing `Testcontainers.PostgreSql` v4.10.0 supports custom images |
| xUnit v3 async lifetime | `IAsyncLifetime.InitializeAsync()` returns `ValueTask`; disposal via `override async ValueTask DisposeAsync()` on `WebApplicationFactory` |
| Auth claim injection | `TestAuthHandler` injects `ClaimTypes.NameIdentifier` (UserId), `ClaimTypes.Role[]`, `"organization_id"` — matches `KeycloakIdentityManager.GetCurrentUser()` claim keys |
| Rebus startup in tests | `services.RemoveAll<IHostedService>()` — prevents all background services (Rebus, OTEL) from starting in the test server; `IBus` replaced with `NSubstitute.Substitute.For<IBus>()` |
| Azure Blob Storage in tests | `IQRCodeService` replaced with NSubstitute mock returning a fixed URL — prevents calls to Azurite/Azure |
| DbContext override | `RemoveAll<ToTenContext>`, `RemoveAll<DbContextOptions<ToTenContext>>`, `RemoveAll<IDbContextFactory<ToTenContext>>` + supply `ConnectionStrings:ToTenDB` in `ConfigureAppConfiguration` so Aspire's Npgsql validation passes against the test container |
| Robot Framework scope | Post-deploy CI job targeting live ACA FQDN — not run on PRs |
| JMeter scope | Artifact-only CI job post-deploy; `continue-on-error: true`; no pass/fail gate yet |

---

## Job Graph (extended from Phase 4)

```
push/PR to main
       │
       ├─── lint-test (always)            ← integration tests run here
       │         │
       │    docker-build-push
       │         │
       │    terraform
       │         │
       │    deploy (main only)
       │         │
       │    ├─── dast-scan (main only)
       │    ├─── robot-tests (main only)  ← NEW: Robot Framework post-deploy
       │    └─── performance-test (main only) ← NEW: JMeter baseline
       │
       └─── nuget-publish (main only, parallel)
```

---

## New Files

| File | Purpose |
|---|---|
| `tests/ToTen.Api.IntegrationTests/Categories/CategoriesEndpointsTests.cs` | 2 tests — `GetCategories` (200), no-auth allowed |
| `tests/ToTen.Api.IntegrationTests/Manifests/ManifestsEndpointsTests.cs` | 5 tests — create, 401 gate, generate QR, associate boxes, unowned box 403 |
| `tests/ToTen.Api.IntegrationTests/Marketplace/MarketplaceEndpointsTests.cs` | 6 tests — create listing, unowned item 403, search (no auth), filter, submit offer, accept offer |
| `tests/ToTen.Api.IntegrationTests/Storage/StorageEndpointsTests.cs` | 5 tests — create location, geometry round-trip, move item, unowned location 403, 401 gate |
| `tests/ToTen.Api.IntegrationTests/Organizations/OrganizationsEndpointsTests.cs` | 4 tests — create, get with auth, 401 gate, delete by owner |
| `tests/ToTen.Api.IntegrationTests/Memberships/MembershipsEndpointsTests.cs` | 3 tests — invite by owner, remove by owner, invite by non-owner 403 |
| `tests/ToTen.Api.IntegrationTests/Users/UsersEndpointsTests.cs` | 3 tests — get with admin role (200), get with user role (403), update roles with admin (200) |
| `tests/ToTen.Api.IntegrationTests/Security/AuthorizationContractTests.cs` | `[Theory]` 401 for all protected routes; `[Theory]` 403 AdminPolicy for non-admin roles; ownership 403 |
| `tests/ToTen.Api.IntegrationTests/Security/OrganizationAccessTests.cs` | 3 tests — member access (200), non-member (403), cross-tenant (403) |
| `tests/ToTen.Api.IntegrationTests/Communications/ChatHubTests.cs` | 3 tests — connect authenticated, unauthenticated throws, send message dispatches to receiver |
| `tests/ToTen.AcceptanceTests/requirements.txt` | `robotframework==7.2`, `robotframework-requests==0.9.7` |
| `tests/ToTen.AcceptanceTests/resources/keywords.resource` | Suite Setup/Teardown, `Authenticated POST` keyword |
| `tests/ToTen.AcceptanceTests/tests/items.robot` | Get 200, Create 201, nonexistent 404 |
| `tests/ToTen.AcceptanceTests/tests/marketplace.robot` | Search 200, price filter returns filtered results |
| `tests/ToTen.AcceptanceTests/tests/organizations.robot` | Create 201, no-auth 401 |
| `tests/performance/move-item-baseline.jmx` | JMeter plan — 10 threads, 10s ramp, 100 iterations against `POST /items/{id}/move` |

## Modified Files

| File | Change |
|---|---|
| `tests/ToTen.Api.IntegrationTests/ToTen.Api.IntegrationTests.csproj` | Added `Microsoft.AspNetCore.SignalR.Client` v10.0.1 |
| `tests/ToTen.Api.IntegrationTests/Helpers/TestAuthOptions.cs` | Added `UserId`, `Roles[]`, `OrganizationId` properties |
| `tests/ToTen.Api.IntegrationTests/Helpers/TestAuthHandler.cs` | Injects `NameIdentifier`, `Role`, `organization_id` claims |
| `tests/ToTen.Api.IntegrationTests/Helpers/ToTenWebApplicationFactory.cs` | Self-contained factory: PostGIS container, `DefaultTestUserId`, client helpers, full DI override |
| `tests/ToTen.Api.IntegrationTests/Items/ItemsEndpointsTests.cs` | Routes fixed (`/items` not `/api/items`), `OwnerId = DefaultTestUserId`, fresh context for delete assertion |
| `src/ToTen.Api/Features/Items/GetItems/GetItemEndpoint.cs` | Route `"/items"` → `"/"` (route group prefix duplication bug) |
| `src/ToTen.Api/Features/Items/GetItem/GetItemEndpoint.cs` | Route `"/items/{id}"` → `"/{id}"` |
| `src/ToTen.Api/Features/Items/CreateItem/CreateItemEndpoint.cs` | Route `"/items"` → `"/"` |
| `src/ToTen.Api/Features/Items/UpdateItem/UpdateItemEndpoint.cs` | Route `"/items/{id}"` → `"/{id}"` |
| `.github/workflows/azure-dev.yml` | Added `robot-tests` and `performance-test` jobs after `deploy` |
| `phases/5/checkpoint.md` | All items marked `[x]` |

---

## Test Infrastructure Design

### `ToTenWebApplicationFactory`

```
IAsyncLifetime.InitializeAsync()  → _db.StartAsync()
ConfigureWebHost()
  ConfigureAppConfiguration       → inject Auth:*, ConnectionStrings:ToTenDB = _db.GetConnectionString()
  ConfigureServices
    RemoveAll<ToTenContext / DbContextOptions / IDbContextFactory>
    AddDbContextFactory<ToTenContext>(UseNpgsql + UseAsyncSeeding)
    RemoveAll<IEventPublisher>     → MockEventPublisher
    RemoveAll<IHostedService>      → prevents Rebus/OTEL startup crash
    RemoveAll<IBus>                → Substitute.For<IBus>()
    AddSingleton<IQRCodeService>   → NSubstitute mock (fixed blob URL)
    Default TestScheme (auth succeeds, DefaultTestUserId)

CreateAuthenticatedClient(roles, userId, orgId, email)
  → WithWebHostBuilder override of auth scheme → CreateClient()

CreateUnauthenticatedClient()
  → WithAuth(succeeds: false) → CreateClient()

WithAuth(succeeds, roles, userId, orgId, email)
  → returns WebApplicationFactory<Program> for SignalR HubConnectionBuilder use
```

### `TestAuthHandler` claim map

| Claim type | Source |
|---|---|
| `ClaimTypes.NameIdentifier` | `Options.UserId` |
| `JwtRegisteredClaimNames.Email` | `Options.Email` |
| `ClaimTypes.Role` (one per entry) | `Options.Roles[]` |
| `"organization_id"` | `Options.OrganizationId` (if set) |

---

## Security Contract Coverage

### 401 Theory — protected routes (unauthenticated client)

| Route | Method |
|---|---|
| `/items` | POST |
| `/items/{id}` | PUT |
| `/items/{id}` | DELETE |
| `/manifests` | POST |
| `/locations` | POST |
| `/items/{id}/move` | POST |
| `/organizations` | POST |
| `/organizations/{id}` | GET |
| `/users` | GET |
| `/users/{id}/roles` | PUT |

### 403 AdminPolicy Theory — non-admin roles → `GET /users`

Roles under test: `["user"]`, `["business_owner"]`, `["third_party"]`

### 403 Ownership — cross-user resource access

Item seeded with `OwnerId = differentUserId`; `DefaultTestUserId` calls `DELETE /items/{id}` → 403.

---

## Robot Framework CI Job

```yaml
robot-tests:
  runs-on: ubuntu-latest
  needs: [deploy, terraform]
  if: github.ref == 'refs/heads/main'
  steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-python@v5
      with: { python-version: '3.11' }
    - run: pip install -r tests/ToTen.AcceptanceTests/requirements.txt
    - run: |
        robot --outputdir test-results/robot \
              --variable BASE_URL:https://${{ needs.terraform.outputs.api_fqdn }} \
              tests/ToTen.AcceptanceTests/
      env: { API_KEY: ${{ secrets.ROBOT_API_KEY }} }
      continue-on-error: true
    - uses: actions/upload-artifact@v4
      if: always()
      with:
        name: robot-framework-results
        path: test-results/robot/
```

---

## JMeter CI Job

```yaml
performance-test:
  runs-on: ubuntu-latest
  needs: [deploy, terraform]
  if: github.ref == 'refs/heads/main'
  steps:
    - uses: actions/checkout@v4
    - name: Download JMeter
      run: |
        wget -q https://downloads.apache.org/jmeter/binaries/apache-jmeter-5.6.3.tgz
        tar -xzf apache-jmeter-5.6.3.tgz
    - name: Run baseline
      run: |
        apache-jmeter-5.6.3/bin/jmeter -n \
          -t tests/performance/move-item-baseline.jmx \
          -Jbase_url=https://${{ needs.terraform.outputs.api_fqdn }} \
          -l jmeter-results.jtl -e -o jmeter-report/
      continue-on-error: true
    - uses: actions/upload-artifact@v4
      if: always()
      with:
        name: jmeter-baseline-report
        path: jmeter-report/
```

---

## Required GitHub Actions Setup (Manual)

| Name | Type | Value |
|---|---|---|
| `ROBOT_API_KEY` | Secret | Bearer token for Robot Framework tests against live ACA |

---

## Verification

**Integration tests (local):**
```bash
dotnet test tests/ToTen.Api.IntegrationTests --configuration Release \
  --logger "trx;LogFileName=results.trx"
```
Expected: all tests pass; `postgis/postgis:17-3.4` container spins up and tears down; no orphan containers after run.

**Robot Framework (local, requires app running):**
```bash
pip install -r tests/ToTen.AcceptanceTests/requirements.txt
robot --variable BASE_URL:http://localhost:5000 tests/ToTen.AcceptanceTests/
```

**JMeter (local, requires app running):**
```bash
jmeter -n -t tests/performance/move-item-baseline.jmx \
  -Jbase_url=http://localhost:5000 -l results.jtl -e -o report/
```

---

## Multi-Env Extension Notes

- Robot Framework `BASE_URL` and `API_KEY` are pipeline variables — no test code change needed for staging vs prod
- JMeter `-Jbase_url` is a JMeter property — parameterised at CI invocation, not inside the `.jmx`
- Security contract `[MemberData]` route list can be extended as new endpoints are added with no structural change to the test class
