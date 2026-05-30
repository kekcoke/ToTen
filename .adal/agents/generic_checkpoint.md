# Generic Agent Checkpoint Template (All Phases)

## Overview
Agents MUST validate all items in their assigned phase checklist before declaring their task complete and handing off to the next agent. Proof of completion (logs, test outputs, etc.) is required.

---

## Phase 4: DevSecOps & CI/CD Integration (DevSecOps Agent)
**Validation Checklist**:
- [ ] YAML syntax is valid and passes CI linting.
- [ ] Security scanners (SAST/DAST) run without breaking the build unexpectedly (unless critical vulnerabilities are found).
- [ ] The pipeline successfully builds the Docker images and pushes to the configured registry.
- [ ] Infrastructure deployment phase completes successfully in a staging/ephemeral environment.
- [ ] Pipeline successfully packs `ToTen.Shared` and `ToTen.Contracts` and publishes the `.nupkg` files to the designated artifact feed.

---

## Phase 5: Quality Automation Pipeline (QA Engineer Agent)
**Validation Checklist**:
- [ ] Ephemeral PostGIS `Testcontainers` spin up and tear down successfully during the test run; no orphan resources.
- [ ] Integration test suite passes with 100% success rate on the new Vertical Slices; no orphan resources.
- [ ] Robot Framework executes successfully against the local or staging URL.
- [ ] SignalR WebSocket connections pass automated integration testing.
- [ ] JMeter test runs without catastrophic failure and produces a baseline report.
- [ ] Test results are successfully published as pipeline artifacts.
- [ ] Integration tests successfully assert 401/403 HTTP status codes for unauthorized role and resource access.
- [ ] Tests verify that Organization members can access shared resources while non-members are correctly rejected.

---

## Phase 6: Advanced Capabilities & Edge Routing (Backend / Architect Agent)
**Validation Checklist**:
- [ ] YARP Gateway project compiles, runs, and successfully reverse-proxies requests to the underlying API.
- [ ] Gateway enforces configured CORS and Rate Limiting policies (verified via curl/Postman).
- [ ] Worker Service initializes Microsoft Semantic Kernel without dependency resolution errors.
- [ ] The pilot AI workflow can successfully process a mock payload and flag an item for "Human Review".
- [ ] The Human Review endpoint correctly transitions the item state in the database upon approval/rejection.
- [ ] AI workflow successfully analyzes mock lineage data and produces correct categorization/flags.