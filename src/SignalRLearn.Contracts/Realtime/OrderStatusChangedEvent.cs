using SignalRLearn.Contracts.Orders;

namespace SignalRLearn.Contracts.Realtime;

public sealed record OrderStatusChangedEvent(
    OrderDto Order,
    OrderStatus PreviousStatus);
