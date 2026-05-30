# Generic Agent Specification Template (All Phases)

## Overview
This document outlines the overarching specification across all project phases. Agents must locate their specific phase/domain, review the assigned to-dos, and execute them strictly within their boundaries.

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