# SignalR Order Pulse

A small .NET 10 learning application that shows the practical role of SignalR: REST endpoints change backend state, then the backend pushes the resulting state to every connected browser in real time.

## Architecture

```text
Blazor WebAssembly --HTTP--> OrdersController --> InMemoryOrderStore
        ^                              |
        |                              | IHubContext<OrderHub, IOrderClient>
        +-------- SignalR events <-----+
```

The solution contains:

- `SignalRLearn.Api` — ASP.NET Core controllers, in-memory storage, SignalR hub, OpenAPI, and Scalar.
- `SignalRLearn.Client` — standalone Blazor WebAssembly dashboard and SignalR .NET client.
- `SignalRLearn.Contracts` — DTOs, status rules, and the strongly typed hub client contract shared by both sides.
- `SignalRLearn.Api.Tests` — store unit tests and HTTP integration tests.

## Run the application

The default development URLs are fixed so CORS and the client API address agree:

- API: `https://localhost:7232`
- Scalar: `https://localhost:7232/scalar`
- Client: `https://localhost:7005`

Open two terminals from the repository root.

```powershell
dotnet run --project src/SignalRLearn.Api
```

```powershell
dotnet run --project src/SignalRLearn.Client
```

If HTTPS development certificates are not trusted, run `dotnet dev-certs https --trust` once.

## Try the real-time flow

1. Open `https://localhost:7005` in two browser tabs.
2. Open Scalar at `https://localhost:7232/scalar`.
3. Use `PATCH /api/orders/{id}/status` in Scalar to change a Processing order to Shipped.
4. Both browser tabs update without a refresh.

The details page can also change a status. The originating tab receives the HTTP response, while every tab—including the originator—receives the same SignalR event. Client updates are idempotent, so either arrival order is safe.

## API

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/orders` | List orders |
| `GET` | `/api/orders/{id}` | Read one order |
| `POST` | `/api/orders` | Create an order in Processing |
| `PATCH` | `/api/orders/{id}/status` | Move an order to another valid status |

Valid transitions are `Processing -> Shipped -> Delivered`; Processing and Shipped may also move to Cancelled. Delivered and Cancelled are terminal.

## SignalR lessons in the code

- `OrderHub` holds no state. Hubs are transient connection endpoints.
- `OrdersController` publishes through `IHubContext` only after the store mutation succeeds.
- `IOrderClient` makes server-to-client calls strongly typed.
- `OrderRealtimeClient` uses automatic reconnect and refreshes the REST snapshot after reconnection.
- The REST API remains the source of truth; SignalR is the low-latency notification channel.
- SignalR selects WebSockets when possible and falls back automatically when necessary.

## Verify

```powershell
dotnet build SignalRLearn.slnx
dotnet test SignalRLearn.slnx --no-build
```

The data store is intentionally in memory. Restarting the API restores the four demo orders.
