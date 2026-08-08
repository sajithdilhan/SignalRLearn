using SignalRLearn.Contracts.Orders;

namespace SignalRLearn.Api.Services;

public interface IOrderStore
{
    IReadOnlyList<OrderDto> GetAll();

    OrderDto? GetById(Guid id);

    OrderDto Create(CreateOrderRequest request);

    UpdateOrderResult UpdateStatus(Guid id, OrderStatus nextStatus);
}
