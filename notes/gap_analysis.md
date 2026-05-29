# ToTen Inventory Management API Platform - Gap Analysis & Transition Plan

## 1. Executive Summary
The current ToTen backend is a well-structured Vertical Slice Architecture .NET 10 template featuring robust cloud-native capabilities (Aspire, Keycloak, Azure Service Bus, PostgreSQL). However, its domain model is currently limited to a basic `Items` and `Categories` schema. To pivot into a comprehensive **Inventory Management API Platform** supporting home storage, moving manifests, and an e-commerce/barter marketplace, significant domain expansion and infrastructure adjustments are required.

## 2. Gap Analysis

### Current State
*   **Domain:** Simple CRUD for `Items` (Id, Name, CategoryId, Price, ReleaseDate, Description) and `Categories`.
*   **Architecture:** Vertical Slice, Event-Driven (Pub/Sub via Azure Service Bus).
*   **Infrastructure as Code (IaC):** `azd` (Azure Developer CLI) with internal Bicep templates.
*   **Auth:** Keycloak.
*   **Deployment:** GitHub Actions / Azure DevOps triggering `azd provision` & `azd deploy`.

### Target State
*   **Domain 1: Home Storage List:** Requires granular location tracking, tagging, and item condition states.
*   **Domain 2: Moving Manifest Tracking:** Requires QR/barcode integration, containerization (Items inside Boxes), weight/dimension tracking, and manifest lifecycle states.
*   **Domain 3: E-commerce/Barter Marketplace:** Requires user profiles, listings, transactional states (offers/barters), and chat/messaging integration.
*   **IaC:** Terraform for more standardized, cross-platform infrastructure management, replacing `azd` Bicep modules.

---

## 3. Components to Rehaul & Add

### To Be Rehauled
*   **`Items` Entity:** Needs abstraction into a base `InventoryItem` with relationships to physical locations, moving boxes, and market listings.
*   **CI/CD Pipelines:** `.github/workflows/azure-dev.yml` and `.azdo/pipelines/azure-dev.yml` currently use `azd provision`. These must be rewritten to use Terraform (`terraform init`, `terraform plan`, `terraform apply`).

### To Be Added (New Slices)
*   **Locations & Storage Slice:** `Locations` (Rooms, Shelves, Bins), `Containers`.
*   **Manifests Slice:** `Boxes` (QR codes, weight, dimensions, fragility), `Manifests` (Source, Destination, Status).
*   **Marketplace Slice:** `Listings` (Price/Barter preference), `Offers` (Negotiations), `Transactions`.
*   **Profiles Slice:** User ratings, public profiles, shipping addresses.
*   **Terraform Configurations:** `/terraform` directory containing `main.tf`, `variables.tf`, `outputs.tf` for Azure Container Apps, Postgres, Service Bus, and Keycloak setup.

---

## 4. Suggested Production-Grade MVP Platform & Architecture

The MVP should maintain the Aspire developer experience locally while adopting a micro-services friendly modular monolith approach in production, utilizing Azure Container Apps.

### Mermaid Architectural Diagram

```mermaid
architecture-beta
    group azure(cloud)[Azure Cloud]
    
    service gateway(server)[Azure Front Door / Gateway] in azure
    service containerapp(server)[Azure Container App: ToTen API] in azure
    service workerapp(server)[Azure Container App: Worker] in azure
    
    service auth(key)[Keycloak (Container App)] in azure
    service db(database)[Azure Database for PostgreSQL Flexible] in azure
    service bus(queue)[Azure Service Bus] in azure
    service storage(disk)[Azure Blob Storage (Images/QR)] in azure

    gateway:R --> L:containerapp
    gateway:R --> L:auth
    
    containerapp:B --> T:db
    containerapp:R --> L:bus
    workerapp:L --> R:bus
    workerapp:B --> T:db
    
    containerapp:T --> B:storage
```

---

## 5. Roadmap & To-Dos (with Sample Prompts)

### Phase 1: Domain Modeling & Database Migrations
*   [ ] **Task:** Expand the existing `Item` model and add new entities.
    *   *Sample Prompt:* "Create Entity Framework Core models for `Location`, `Box`, and `Listing`. Update the existing `Item` class to include foreign keys to `LocationId` and `BoxId`. Generate a new EF migration."

### Phase 2: Vertical Slices Implementation
*   [ ] **Task:** Implement Home Storage endpoints.
    *   *Sample Prompt:* "Implement a Vertical Slice for `CreateLocation` and `MoveItemToLocation`. Include the Endpoint, DTOs, and Command Handler. Publish an `ItemMovedEvent` to Azure Service Bus."
*   [ ] **Task:** Implement Moving Manifest endpoints.
    *   *Sample Prompt:* "Create endpoints to generate a Moving Manifest. Include a service to generate QR codes for `Boxes` and save them to Azure Blob Storage."

### Phase 3: Terraform Migration
*   [ ] **Task:** Replace `azd` Bicep with Terraform.
    *   *Sample Prompt:* "Write Terraform scripts using the `azurerm` provider to deploy an Azure Container Apps Environment, a PostgreSQL Flexible Server, an Azure Service Bus namespace, and an Azure Key Vault. Match the configuration currently expected by the Aspire manifest."

### Phase 4: CI/CD Pipeline Update
*   [ ] **Task:** Update GitHub Actions and Azure DevOps pipelines.
    *   *Sample Prompt:* "Rewrite the `.github/workflows/azure-dev.yml` to replace the `azd provision` and `azd deploy` steps with HashiCorp's `setup-terraform` action, followed by `terraform apply` and an Azure CLI command to deploy the container images."

---

## 6. Deployment Instructions (Dev Workflow to Production)

Currently, the workflow relies on `azd` (Azure Developer CLI) with Federated Credentials. Transitioning to Terraform alters this flow.

### Updated Dev-to-Prod Workflow
1.  **Local Development:** Developers use `dotnet run --project src/ToTen.AppHost` (Aspire) to develop and test locally.
2.  **Commit & Push:** Code is pushed to the `main` branch.
3.  **Pipeline Triggered (GitHub Actions / Azure DevOps):**
    *   **Checkout & Setup:** Environment sets up .NET 10 and Terraform.
    *   **Build & Test:** `dotnet build` and `dotnet test` are run to ensure integration tests (using Testcontainers) pass.
    *   **Docker Build & Push:** Pipeline builds the API and Worker Docker images and pushes them to Azure Container Registry (ACR).
    *   **Infrastructure Provisioning (Terraform):**
        *   `terraform init`
        *   `terraform plan -out=tfplan`
        *   `terraform apply tfplan` (Provisions/Updates PostgreSQL, Service Bus, ACA Environment).
    *   **Application Deployment:** Pipeline updates the Azure Container App revisions with the newly built image tags from ACR.

### Reference to Existing Pipelines
To implement this, you will need to edit:
*   `@.azdo/pipelines/azure-dev.yml` - Remove `setup-azd@1` and `AzureCLI@2` inline scripts running `azd`. Replace with Terraform tasks and Docker registry pushes.
*   `@.github/workflows/azure-dev.yml` - Remove `Azure/setup-azd@v2` and `azd provision/deploy`. Replace with `hashicorp/setup-terraform@v3`, `docker/build-push-action@v5`, and `azure/container-apps-deploy-action@v1`.
