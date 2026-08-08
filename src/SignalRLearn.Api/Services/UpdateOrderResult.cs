using SignalRLearn.Contracts.Orders;

namespace SignalRLearn.Api.Services;

public enum UpdateOrderOutcome
{
    Updated,
    Unchanged,
    NotFound,
    InvalidTransition
}

public sealed record UpdateOrderResult(
    UpdateOrderOutcome Outcome,
    OrderDto? Order = null,
    OrderStatus? PreviousStatus = null,
    string? Error = null);
