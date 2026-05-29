# Generic Agent Specification Template (All Phases)

## Overview
This document outlines the overarching specification across all project phases. Agents must locate their specific phase/domain, review the assigned to-dos, and execute them strictly within their boundaries.

---

## Phase 4: DevSecOps & CI/CD Integration
**Assigned Agent**: DevSecOps Agent
**Objective**: Automate delivery, enforce quality gates, and embed security by design.
**To-Dos**:
- [ ] Rewrite `.github/workflows/azure-dev.yml` to utilize `hashicorp/setup-terraform`.
- [ ] Add SAST/DAST scanning steps (GitHub Advanced Security / OWASP ZAP) to pull requests.
- [ ] Configure Docker Build & Push to Azure Container Registry (ACR).
- [ ] Implement pipeline deployment steps using Terraform apply and Azure CLI container app updates.

- [ ] Configure CI pipelines to pack and publish `Shared` and `Contracts` as reusable internal NuGet packages.

---

## Phase 5: Quality Automation Pipeline
**Assigned Agent**: QA Engineer Agent
**Objective**: Ensure near-100% automated regression and system testing via ephemeral environments.
**To-Dos**:
- [ ] Set up **Testcontainers** integration with PostGIS-enabled images for isolated database testing.
- [ ] Write API integration tests covering all Phase 2 Vertical Slices.
- [ ] Scaffold Robot Framework for behavior-driven black-box testing.
- [ ] Write integration tests for SignalR Hubs using ASP.NET Core `TestServer` WebSocket client.
- [ ] Create a baseline JMeter load test for the `MoveItemToLocation` endpoint.
- [ ] Ensure all test outputs are formatted for CI/CD pipeline ingestion.
- [ ] Write API contract tests specifically validating 401 (Unauthorized) and 403 (Forbidden) boundaries for the 6-tier roles and cross-tenant data access.
- [ ] Write multi-user interaction tests validating resource sharing within Organizations and proper 403 denials for non-members.

---

## Phase 6: Advanced Capabilities & Edge Routing
**Assigned Agent**: Backend Developer Agent / Architect Agent
**Objective**: Introduce AI-driven workflows and establish an API Gateway for edge routing.
**To-Dos**:
- [ ] Create a new `ToTen.Gateway` project in the Aspire AppHost using YARP.
- [ ] Move CORS, Rate Limiting, and basic request validation to the gateway layer.
- [ ] Integrate Microsoft Semantic Kernel into the Worker Service.
- [ ] Define a pilot AI workflow for automated data categorization or anomaly detection.
- [ ] Implement a "Human Review" approval endpoint for flagged items.
- [ ] Utilize Semantic Kernel to analyze lineage data for depreciation, wash trading detection, or vintage badge generation.