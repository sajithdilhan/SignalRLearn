using System.ComponentModel.DataAnnotations;

namespace SignalRLearn.Contracts.Orders;

public sealed class UpdateOrderQuantityRequest
{
    [Range(1, 1000)]
    public int Quantity { get; set; }
}
