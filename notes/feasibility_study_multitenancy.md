# Feasibility Study: Multi-Tenancy vs. Single-Tenant Architecture for the ToTen Platform

**Prepared By:** Product & R&D Consulting
**Date:** May 2026
**Subject:** Architectural strategy for ToTen (Inventory, Manifests, and Marketplace Platform)

---

## 1. Executive Summary
The ToTen platform is evolving from a simple inventory CRUD application into a comprehensive ecosystem supporting Home Storage, Moving Manifests, and a C2C/B2B Marketplace with real-time chat, geolocation, and group management. 

This study evaluates whether the platform should transition into a **True Multi-Tenant Architecture** (hard data isolation per business/client) or remain a **Single-Tenant Architecture with Logical Grouping** (where users and businesses share the same database and schema, isolated only by application-level authorization rules).

Our analysis concludes that while true multi-tenancy is the gold standard for pure B2B SaaS, a **Single-Tenant ecosystem with robust Logical Grouping (Organization-based RBAC)** is the superior choice for ToTen's unique blend of B2B (Moving/Storage companies) and B2C (Marketplace/Homeowners) capabilities.

---

## 2. Market Landscape & Gap Analysis

### Current Market Segments
1. **Home Inventory Apps (e.g., Sortly, Nest Egg):** Heavily B2C or small business. Siloed data. Lacks native marketplace off-ramps for unwanted goods.
2. **Moving & Logistics SaaS (e.g., Supermove, SmartMoving):** Pure B2B multi-tenant SaaS. Excellent at dispatch and manifests, but consumer interaction is limited to read-only portals.
3. **C2C Marketplaces (e.g., Facebook Marketplace, OfferUp):** Massive network effects, but zero integration with structured home inventory or moving manifests.

### The Identified Gap
There is a distinct gap for a **Unified Lifecycle Platform**. Consumers want to catalog their home (Inventory), pack it for a move or storage (Manifests), and instantly liquidate unwanted items (Marketplace). Furthermore, they want to seamlessly grant temporary access to B2B service providers (Moving Companies) to view their manifests without exporting data to a third-party silo.

### Impact on Architecture
If ToTen adopts *True Multi-Tenancy* (where each moving company or retailer gets their own isolated database/schema), cross-tenant data sharing becomes exceptionally difficult. A unified marketplace requires a shared data pool.

---

## 3. Architectural Evaluation

### Option A: As-Is Single-Tenant (with Organization/Group RBAC)
In this model, the database is shared. "Households" and "Businesses" exist as `Organization` entities within the same schema. Access is governed strictly by the `IAuthorizationHandler` checking `OwnerId` and `OrganizationMembership`.

*   **Pros:**
    *   **Marketplace Network Effects:** Easy to query all public listings across the entire platform globally.
    *   **Data Fluidity:** A homeowner can easily share a specific `Manifest` with a Moving Company (`Organization`) by simply adding a temporary access record.
    *   **Operational Simplicity:** Only one set of infrastructure to deploy, monitor, and scale via Azure Container Apps and Flexible PostgreSQL.
*   **Cons:**
    *   **Noisy Neighbors:** A heavy B2B user doing bulk inventory imports could consume DB resources, impacting consumer marketplace performance.
    *   **Data Privacy Perception:** Enterprise B2B clients may hesitate if their data is in the exact same table as consumer data (requires rigorous SOC2 compliance).

### Option B: True Multi-Tenancy (B2B SaaS Model)
In this model, the application routes requests based on a `TenantId`. Data is isolated via Row-Level Security (RLS), separate PostgreSQL schemas, or even completely separate Azure PostgreSQL databases per tenant.

*   **Pros:**
    *   **Enterprise Appeal:** B2B moving companies and storage facilities prefer hard data isolation.
    *   **Customization:** Easier to offer white-labeled portals, custom Keycloak identity realms, and bespoke schema extensions per tenant.
    *   **Fault Isolation:** A database crash for Tenant A does not affect Tenant B.
*   **Cons:**
    *   **Marketplace Fragmentation:** Building a global marketplace becomes highly complex. You have to aggregate queries across thousands of tenant schemas, or build an asynchronous data pipeline replicating "public listings" to a central marketplace database.
    *   **Identity Friction:** Users would need different accounts for their personal Home Inventory vs. interacting with a specific Moving Company.
    *   **Engineering Overhead:** Requires massive changes to EF Core (Global Query Filters), database provisioning, and Terraform complexity.

---

## 4. Key Considerations

1. **The Product Vision:** Is ToTen primarily a *B2B software sold to moving companies*? Or is it a *Consumer Platform with B2B features*? 
    *   If the value proposition is the **Marketplace** and network effects, data must live together.
2. **Cost of Infrastructure:** Multi-tenant databases (even logical ones using schemas) dramatically increase the active connection pools and memory footprint required for PostgreSQL.
3. **Compliance & Security:** Single-tenant relying on application-level authorization is prone to data leakage if a developer makes a mistake in an API endpoint. DevSecOps automated testing (Phase 5) is non-negotiable here.

---

## 5. Final Recommendations

**Recommendation: Proceed with the As-Is Single-Tenant Architecture with Logical Grouping.**

**Rationale:**
ToTen's unique differentiator is the intersection of Personal Inventory, B2B Manifesting, and a global Marketplace. Implementing True Multi-Tenancy would create artificial data silos that cripple the Marketplace's network effects and complicate consumer-to-business data sharing.

**Strategic Mitigation (The Hybrid Approach):**
To address the drawbacks of a Single-Tenant system, the engineering team should adopt the following strategies:
1. **Row-Level Security (RLS) in PostgreSQL:** Even in a single database, implement Postgres RLS policies based on `OrganizationId` to create a hard database-level safeguard against cross-organizational data leakage, acting as a safety net below the application's `IAuthorizationHandler`.
2. **Dedicated YARP Gateways:** Use the API Gateway to rate-limit aggressive B2B traffic separately from standard consumer traffic to prevent "noisy neighbor" scenarios.
3. **Dynamic Schema Engine (Phase 1):** The planned JSONB dynamic schema is sufficient to satisfy B2B clients needing custom fields without requiring entirely separate tenant databases.

By remaining technically Single-Tenant but structurally segmented via Organizations, ToTen can capture the B2B SaaS revenue stream while maintaining the liquidity required for a thriving C2C Marketplace.