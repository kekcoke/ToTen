# Phase 2: Messaging, Search & Identity Implementation Plan
**Date**: 2026-05-29  
**POC**: AdaL (Backend Agent)  
**Status**: Infrastructure Implemented / Implementing Slices

## 1. Research Questions & Objectives
- **Rebus Topic Mapping**: How to map standalone `record` events (e.g., `ItemMovedEvent`) to ASB Topics/Subscriptions. Rebus auto-maps .NET types to topics by default; custom routing available via `TypeBased` conventions.
- **Throttling & Scaling**: Configuring independent consumers for high-volume vs. low-frequency events.
- **SDK Fallback**: Designing a decoupled `ServiceBusProcessor` pattern for isolated handlers.
- **Real-time & Identity**: Integrating `IIdentityManager` with SignalR `ChatHub` using JWT Bearer tokens.

## 2. Technical Dimensions
### Messaging (Rebus)
- **Topic/Queue Mapping**: Using `Rebus.AzureServiceBus` transport with `UseAzureServiceBus` for topic-based pub/sub. Custom topic naming via `TypeBased` conventions when needed.
- **Handler Registration**: Using `AddRebusHandler<T>()` for explicit handler registration. Handlers implement `IHandleMessages<T>` with `Task Handle(T message)`.

### SignalR & Identity
- **IIdentityManager**: Abstraction to handle Keycloak/JWT logic.
- **WebSocket Auth**: Middleware or Hub configuration to extract tokens from query strings (standard for SignalR).

## 3. Implementation Steps
1.  **Research & Prototyping**: [DONE] Originally investigated MassTransit's `SetEntityName`/`ConfigureEndpoints`. **Migrated to Rebus** (8.9.2) with `Rebus.AzureServiceBus` (10.6.0) and `Rebus.ServiceProvider` (10.7.2) — all compatible with .NET 10.
2.  **Code Generation**:
    - `ToTen.Contracts`: [DONE] Define Phase 2 events.
    - `ToTen.Infrastructure`: [DONE] Implement `IIdentityManager` and Rebus registration (`AddToTenRebus`).
    - `ToTen.Api`: [DONE] SignalR Hub and Middleware setup.
3.  **Verification**: [PENDING] Write integration tests for event publishing and hub connectivity.

## 4. Expected Output
A set of production-ready C# files including:
- `RebusConfiguration.cs` (Rebus setup, renamed from `MassTransitConfiguration.cs`)
- `ServiceBusProcessorFactory.cs` (Fallback pattern)
- `IdentityAndSignalRConfiguration.cs` (DI and Hub setup)
