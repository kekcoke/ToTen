# Phase 2: Messaging, Search & Identity Implementation Plan
**Date**: 2026-05-29  
**POC**: AdaL (Backend Agent)  
**Status**: Infrastructure Implemented / Implementing Slices

## 1. Research Questions & Objectives
- **MassTransit Topology**: How to map standalone `record` events (e.g., `ItemMovedEvent`) to ASB Topics/Subscriptions without a shared base interface.
- **Throttling & Scaling**: Configuring independent consumers for high-volume vs. low-frequency events.
- **SDK Fallback**: Designing a decoupled `ServiceBusProcessor` pattern for isolated handlers.
- **Real-time & Identity**: Integrating `IIdentityManager` with SignalR `ChatHub` using JWT Bearer tokens.

## 2. Technical Dimensions
### Messaging (MassTransit)
- **Convention-based Mapping**: Using `MessageTopology` to automate topic/subscription naming.
- **Consumer Scoping**: Using `AddConsumer<T>` with specific `ReceiveEndpoint` configurations to allow independent scaling.

### SignalR & Identity
- **IIdentityManager**: Abstraction to handle Keycloak/JWT logic.
- **WebSocket Auth**: Middleware or Hub configuration to extract tokens from query strings (standard for SignalR).

## 3. Implementation Steps
1.  **Research & Prototyping**: [DONE] Investigate MassTransit's `SetEntityName` and `ConfigureEndpoints` for standalone records.
2.  **Code Generation**:
    - `ToTen.Contracts`: [DONE] Define Phase 2 events.
    - `ToTen.Infrastructure`: [DONE] Implement `IIdentityManager` and MassTransit registration.
    - `ToTen.Api`: [DONE] SignalR Hub and Middleware setup.
3.  **Verification**: [PENDING] Write integration tests for event publishing and hub connectivity.

## 4. Expected Output
A set of production-ready C# files including:
- `MessagingConfiguration.cs` (MassTransit setup)
- `ServiceBusProcessorFactory.cs` (Fallback pattern)
- `IdentityAndSignalRConfiguration.cs` (DI and Hub setup)
