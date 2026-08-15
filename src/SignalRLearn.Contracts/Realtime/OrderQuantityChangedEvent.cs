using SignalRLearn.Contracts.Orders;

namespace SignalRLearn.Contracts.Realtime;

public sealed record OrderQuantityChangedEvent(
    OrderDto Order,
    int PreviousQuantity);
