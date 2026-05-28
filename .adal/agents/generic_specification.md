# Generic Agent Specification Template (All Phases)

## Overview
This document outlines the overarching specification across all project phases. Agents must locate their specific phase/domain, review the assigned to-dos, and execute them strictly within their boundaries.

---

## Phase 1: Domain Modeling & DB Migrations
**Assigned Agent**: Architect Agent
**Objective**: Expand the base `Items` domain into a comprehensive inventory system.
**To-Dos**:
- [ ] Refactor `Item` to a base `InventoryItem` entity.
- [ ] Create EF Core models for the new **Storage Slice**: `Location`, `Container`/`Box`.
- [ ] Create EF Core models for the **Manifest Slice**: `Manifest` (Source, Destination, Status).
- [ ] Create EF Core models for the **Marketplace Slice**: `Listing`, `Offer`, `Transaction`.
- [ ] Establish foreign key relationships (e.g., `InventoryItem` -> `LocationId`, `BoxId`).
- [ ] Introduce an `ItemLineage` (or `ItemLedger`) entity to track ownership and condition history.
- [ ] Implement dynamic JSONB schema support for extensible attributes.
- [ ] Enable PostGIS extension, add NetTopologySuite, add spatial Point Coordinates to Location, and configure GIST/GIN indexes.
- [ ] Add `OwnerId` and `OrganizationId` to `InventoryItem` and `Location` to support Resource-Based Authorization.
- [ ] Update Keycloak Realm configuration (`ToTen-realm.json`) to define the 6-tier role model (`user`, `business_owner`, `internal_user`, `admin`, `super_admin`, `third_party`).
- [ ] Create models for `ChatThread`, `ChatMessage`, `Notification`, and `NotificationPreference`.
- [ ] Generate the initial Entity Framework Core migration (`dotnet ef migrations add ExpandDomain`).

---

## Phase 2: Vertical Slices Implementation
**Assigned Agent**: Backend Developer Agent
**Objective**: Implement the application logic, routing, and asynchronous workflows for the new domains.
**To-Dos**:
- [ ] Implement **Home Storage Endpoints**: `CreateLocation`, `MoveItemToLocation`.
- [ ] Implement **Manifest Endpoints**: Generate Moving Manifest, associate boxes.
- [ ] Implement QR Code generation service for Manifest boxes (save to Azure Blob Storage).
- [ ] Implement **Marketplace Endpoints**: Create listing, submit offer.
- [ ] Implement Advanced Search endpoint supporting Geolocation (distance/radius), Text Search, and faceted filtering/sorting.
- [ ] Modify Marketplace `Transaction` logic to write immutable records to `ItemLineage` and publish `ItemTransferredEvent`.
- [ ] Define and implement Message Contracts for Azure Service Bus/MassTransit (e.g., `ItemMovedEvent`, `ManifestCreatedEvent`).
- [ ] Implement Background Worker Consumers for the newly created events.
- [ ] Implement a `Communications` vertical slice with an ASP.NET Core SignalR `ChatHub`.
- [ ] Implement Worker consumers for `SendNotificationEvent` translating to SendGrid (Email) or Twilio (SMS).
- [ ] Abstract Keycloak/Identity logic behind a generic `IIdentityManager` interface (Pluggable IAM).
- [ ] Implement ASP.NET Core Authorization Policies corresponding to the 6 roles.
- [ ] Implement `IAuthorizationHandler` for Resource-Based Authorization (verifying data ownership).
- [ ] Implement a Users/Roles vertical slice for Administrators to manage user permissions.
- [ ] Implement `Organizations` and `Memberships` vertical slices (Create/Update/Delete groups, Invite/Remove members).
- [ ] Update `IAuthorizationHandler` to support hierarchical access (OwnerId OR active OrganizationMembership).

---

## Phase 3: Infrastructure as Code (Terraform) Migration
**Assigned Agent**: Architect Agent / DevSecOps Agent
**Objective**: Replace `azd` Bicep templates with enterprise-grade Terraform modules.
**To-Dos**:
- [ ] Create `/terraform` directory structure (`main.tf`, `variables.tf`, `outputs.tf`).
- [ ] Write Azure Container Apps Environment module.
- [ ] Write Azure Database for PostgreSQL Flexible Server module (must support PostGIS extension).
- [ ] Write Azure Service Bus namespace and Keycloak container modules.
- [ ] Write Azure SignalR Service Terraform module and configure API keys for external providers.
- [ ] Configure OpenTelemetry (OTLP) infrastructure endpoints.

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