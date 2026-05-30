## Phase 5: Quality Automation Pipeline
**Assigned Agent**: QA Engineer Agent
**Objective**: Ensure near-100% automated regression and system testing via ephemeral environments.
**To-Dos**:
- [x] Fix `ToTenWebApplicationFactory` blockers: remove `IDbContextOptionsConfiguration<ToTenContext>` (Aspire validation hook), inject `ConnectionStrings:ToTenDB` into `ConfigureAppConfiguration`, and use `PostConfigure<TestAuthOptions>` in `WithAuth()` to prevent "Scheme already exists" on derived factories.
- [x] Set up **Testcontainers** integration with PostGIS-enabled images for isolated database testing.
- [x] Write API integration tests covering all Phase 2 Vertical Slices (57 tests across Categories, Organizations, Users, Memberships, Storage, Manifests, Marketplace — 57/57 passing).
- [x] Scaffold Robot Framework for behavior-driven black-box testing (`items.robot`, `marketplace.robot`, `organizations.robot` against live ACA; `BASE_URL`/`API_KEY` injected via CI).
- [x] Write integration tests for SignalR Hubs using ASP.NET Core `TestServer` WebSocket client (3 tests: connect authenticated, connect unauthenticated throws, send message fires `ReceiveMessage`).
- [x] Create a baseline JMeter load test for the `MoveItemToLocation` endpoint (10 threads, 10s ramp, 100 iterations; artifact-only, no pass/fail gate).
- [x] Ensure all test outputs are formatted for CI/CD pipeline ingestion (`.trx` upload, Robot Framework results artifact, JMeter HTML report artifact).
- [x] Write API contract tests specifically validating 401 (Unauthorized) and 403 (Forbidden) boundaries for the 6-tier roles and cross-tenant data access (14 × 401 theory rows + 3 × 403 AdminPolicy theory rows). Document Items CRUD security gap (endpoints currently public — no `RequireAuthorization`).
- [x] Write multi-user interaction tests validating resource sharing within Organizations and proper 403 denials for non-members (3 org-access tests: owner access, non-member 403, cross-tenant 403).
- [x] Add `robot-tests` (job 6) and `performance-test` (job 7) to the CI pipeline — post-deploy, main-only, both `continue-on-error: true` with artifact upload.
