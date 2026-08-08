using SignalRLearn.Contracts.Orders;

namespace SignalRLearn.Client.Services;

public sealed class OrderState
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, OrderDto> _orders = [];

    public event Action? Changed;

    public IReadOnlyList<OrderDto> Orders
    {
        get
        {
            lock (_gate)
            {
                return _orders.Values
                    .OrderByDescending(order => order.UpdatedAtUtc)
                    .ToArray();
            }
        }
    }

    public OrderDto? GetById(Guid id)
    {
        lock (_gate)
        {
            return _orders.GetValueOrDefault(id);
        }
    }

    public void MergeSnapshot(IEnumerable<OrderDto> orders)
    {
        var changed = false;
        lock (_gate)
        {
            foreach (var order in orders)
            {
                if (!_orders.TryGetValue(order.Id, out var current) || order.UpdatedAtUtc >= current.UpdatedAtUtc)
                {
                    _orders[order.Id] = order;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    public void Upsert(OrderDto order)
    {
        var changed = false;
        lock (_gate)
        {
            if (!_orders.TryGetValue(order.Id, out var current) || order.UpdatedAtUtc >= current.UpdatedAtUtc)
            {
                _orders[order.Id] = order;
                changed = true;
            }
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }
}
