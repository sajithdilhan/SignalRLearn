using SignalRLearn.Contracts.Orders;

namespace SignalRLearn.Api.Services;

public sealed class InMemoryOrderStore : IOrderStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, StoredOrder> _orders = [];
    private readonly TimeProvider _timeProvider;

    public InMemoryOrderStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        SeedDemoOrders();
    }

    public IReadOnlyList<OrderDto> GetAll()
    {
        lock (_gate)
        {
            return _orders.Values
                .Select(ToDto)
                .OrderByDescending(order => order.UpdatedAtUtc)
                .ToArray();
        }
    }

    public OrderDto? GetById(Guid id)
    {
        lock (_gate)
        {
            return _orders.TryGetValue(id, out var order) ? ToDto(order) : null;
        }
    }

    public OrderDto Create(CreateOrderRequest request)
    {
        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            var order = new StoredOrder
            {
                Id = Guid.NewGuid(),
                CustomerName = request.CustomerName.Trim(),
                ProductName = request.ProductName.Trim(),
                Quantity = request.Quantity,
                Status = OrderStatus.Processing,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _orders.Add(order.Id, order);
            return ToDto(order);
        }
    }

    public UpdateOrderResult UpdateStatus(Guid id, OrderStatus nextStatus)
    {
        lock (_gate)
        {
            if (!_orders.TryGetValue(id, out var order))
            {
                return new(UpdateOrderOutcome.NotFound);
            }

            var previousStatus = order.Status;
            if (previousStatus == nextStatus)
            {
                return new(UpdateOrderOutcome.Unchanged, ToDto(order), previousStatus);
            }

            if (!previousStatus.CanTransitionTo(nextStatus))
            {
                return new(
                    UpdateOrderOutcome.InvalidTransition,
                    ToDto(order),
                    previousStatus,
                    $"An order cannot move from {previousStatus} to {nextStatus}.");
            }

            order.Status = nextStatus;
            order.UpdatedAtUtc = _timeProvider.GetUtcNow();

            return new(UpdateOrderOutcome.Updated, ToDto(order), previousStatus);
        }
    }

    public UpdateOrderResult UpdateQuantity(Guid id, int quantity)
    {
        lock (_gate)
        {
            if (!_orders.TryGetValue(id, out var order))
            {
                return new(UpdateOrderOutcome.NotFound);
            }

            if (order.Quantity == quantity)
            {
                return new(UpdateOrderOutcome.Unchanged, ToDto(order), PreviousQuantity: order.Quantity);
            }

            if (order.Status != OrderStatus.Processing)
            {
                return new(
                    UpdateOrderOutcome.InvalidTransition,
                    ToDto(order),
                    Error: $"An order's quantity cannot be changed while it is {order.Status}.");
            }

            var previousQuantity = order.Quantity;
            order.Quantity = quantity;
            order.UpdatedAtUtc = _timeProvider.GetUtcNow();

            return new(UpdateOrderOutcome.Updated, ToDto(order), PreviousQuantity: previousQuantity);
        }
    }

    private void SeedDemoOrders()
    {
        var now = _timeProvider.GetUtcNow();
        AddSeed("Maya Chen", "Wireless keyboard", 1, OrderStatus.Processing, now.AddMinutes(-18), now.AddMinutes(-18));
        AddSeed("Noah Williams", "Noise-cancelling headphones", 2, OrderStatus.Shipped, now.AddHours(-3), now.AddMinutes(-32));
        AddSeed("Aisha Rahman", "4K monitor", 1, OrderStatus.Delivered, now.AddDays(-1), now.AddHours(-4));
        AddSeed("Leo Martin", "USB-C travel dock", 1, OrderStatus.Cancelled, now.AddDays(-2), now.AddDays(-1));
    }

    private void AddSeed(
        string customerName,
        string productName,
        int quantity,
        OrderStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        var order = new StoredOrder
        {
            Id = Guid.NewGuid(),
            CustomerName = customerName,
            ProductName = productName,
            Quantity = quantity,
            Status = status,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc
        };

        _orders.Add(order.Id, order);
    }

    private static OrderDto ToDto(StoredOrder order) => new(
        order.Id,
        order.CustomerName,
        order.ProductName,
        order.Quantity,
        order.Status,
        order.CreatedAtUtc,
        order.UpdatedAtUtc);

    private sealed class StoredOrder
    {
        public Guid Id { get; init; }
        public required string CustomerName { get; init; }
        public required string ProductName { get; init; }
        public int Quantity { get; set; }
        public OrderStatus Status { get; set; }
        public DateTimeOffset CreatedAtUtc { get; init; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }
}
