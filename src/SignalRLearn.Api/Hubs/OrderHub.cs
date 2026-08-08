using Microsoft.AspNetCore.SignalR;
using SignalRLearn.Contracts.Realtime;

namespace SignalRLearn.Api.Hubs;

// Hubs are transient. State lives in IOrderStore; this hub only represents
// the real-time endpoint and its strongly typed server-to-client contract.
public sealed class OrderHub : Hub<IOrderClient>;
