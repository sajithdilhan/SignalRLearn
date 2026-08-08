using System.ComponentModel.DataAnnotations;

namespace SignalRLearn.Contracts.Orders;

public sealed class UpdateOrderStatusRequest
{
    [EnumDataType(typeof(OrderStatus))]
    public OrderStatus Status { get; set; }
}
