namespace SignalRLearn.Contracts.Orders;

public static class OrderStatusRules
{
    public static bool CanTransitionTo(this OrderStatus current, OrderStatus next) =>
        current == next || (current, next) switch
        {
            (OrderStatus.Processing, OrderStatus.Shipped) => true,
            (OrderStatus.Processing, OrderStatus.Cancelled) => true,
            (OrderStatus.Shipped, OrderStatus.Delivered) => true,
            (OrderStatus.Shipped, OrderStatus.Cancelled) => true,
            _ => false
        };

    public static IReadOnlyList<OrderStatus> ValidNextStatuses(this OrderStatus current) => current switch
    {
        OrderStatus.Processing => [OrderStatus.Shipped, OrderStatus.Cancelled],
        OrderStatus.Shipped => [OrderStatus.Delivered, OrderStatus.Cancelled],
        _ => []
    };
}
