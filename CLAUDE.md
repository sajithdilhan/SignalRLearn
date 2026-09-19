# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A .NET 10 learning app demonstrating SignalR: REST endpoints mutate backend state, then the backend pushes the resulting state to every connected browser in real time. The data store is in-memory only — restarting the API resets to four seeded demo orders.

```text
Blazor WebAssembly --HTTP--> OrdersController --> InMemoryOrderStore
        ^                              |
        |                              | IHubContext<OrderHub, IOrderClient>
        +-------- SignalR events <-----+
```

## Projects

- `src/SignalRLearn.Api` — ASP.NET Core controllers, in-memory storage, SignalR hub, OpenAPI, and Scalar.
- `src/SignalRLearn.Client` — standalone Blazor WebAssembly dashboard and SignalR .NET client.
- `src/SignalRLearn.Contracts` — DTOs, status rules, and the strongly typed hub client contract shared by both sides.
- `tests/SignalRLearn.Api.Tests` — store unit tests, HTTP integration tests (`WebApplicationFactory<Program>`), and FsCheck property tests for the controller.

## Commands

Build and test from the repo root:

```powershell
dotnet build SignalRLearn.slnx
dotnet test SignalRLearn.slnx --no-build
```

Run a single test class or method:

```powershell
dotnet test SignalRLearn.slnx --filter "FullyQualifiedName~OrdersApiTests"
dotnet test SignalRLearn.slnx --filter "FullyQualifiedName~OrdersControllerPropertyTests.SomeTestName"
```

Run the app locally (two terminals from the repo root; dev URLs are fixed so CORS and the client API address agree):

```powershell
dotnet run --project src/SignalRLearn.Api
```

```powershell
dotnet run --project src/SignalRLearn.Client
```

- API: `https://localhost:7232`, Scalar docs at `https://localhost:7232/scalar`
- Client: `https://localhost:7005`
- If HTTPS dev certs aren't trusted: `dotnet dev-certs https --trust`

To exercise the real-time flow manually: open the client in two browser tabs, then use `PATCH /api/orders/{id}/status` in Scalar (or the details page) to change an order's status — both tabs update without a refresh, including the tab that made the request.

## Architecture notes

- **REST is the source of truth; SignalR is the notification channel.** `OrdersController` always mutates via `IOrderStore` first, then publishes the resulting state through `IHubContext<OrderHub, IOrderClient>` — never the other way around. When adding a new mutating endpoint, follow this same order: mutate, then broadcast.
- **`OrderHub` holds no state.** Hubs are transient per-connection endpoints; any shared state lives in `IOrderStore`.
- **`IOrderClient`** (in `SignalRLearn.Contracts`) defines the strongly-typed server-to-client hub methods (e.g. `OrderCreated`, `OrderStatusChanged`, `OrderQuantityChanged`). Both the API's `IHubContext<OrderHub, IOrderClient>` and the client's `OrderRealtimeClient` depend on this shared contract — add new push events here first.
- **Status/quantity transition rules live in `SignalRLearn.Contracts.Orders.OrderStatusRules`**, not in the controller or store. Valid status transitions: `Processing -> Shipped -> Delivered`; `Processing`/`Shipped` may also move to `Cancelled`; `Delivered`/`Cancelled` are terminal. Quantity may only be changed while an order is `Processing`.
- **`InMemoryOrderStore`** is a singleton guarded by a single `Lock` for all reads/writes, and returns an `UpdateOrderResult` (`Outcome`: `NotFound` / `Unchanged` / `InvalidTransition` / `Updated`) so the controller can decide whether to broadcast without re-deriving business rules.
- **Client-side idempotency:** `OrderRealtimeClient` uses SignalR's automatic reconnect and re-fetches the REST snapshot after reconnecting, since events can be missed while disconnected. UI updates from hub events must stay idempotent — the same order/event may arrive via the direct HTTP response and via the broadcast SignalR event, in either order.
- **JSON enum handling:** both the API's controllers (`AddJsonOptions`) and the SignalR hub (`AddJsonProtocol`) register a `JsonStringEnumConverter` — new enum-valued contracts serialize as strings on both transports without extra config.
- Tests mix xUnit `[Fact]`/integration tests (`OrdersApiTests`, against a real `WebApplicationFactory<Program>`) with FsCheck property-based tests (`OrdersControllerPropertyTests`) — prefer property tests for validating `OrderStatusRules` transition logic against arbitrary status pairs.
