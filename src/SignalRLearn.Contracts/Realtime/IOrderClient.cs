using SignalRLearn.Contracts.Orders;

namespace SignalRLearn.Contracts.Realtime;

public interface IOrderClient
{
    Task OrderCreated(OrderDto order);

    Task OrderStatusChanged(OrderStatusChangedEvent update);
}
