namespace SignalRLearn.Contracts.Orders;

public sealed record OrderDto(
    Guid Id,
    string CustomerName,
    string ProductName,
    int Quantity,
    OrderStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
