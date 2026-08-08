using System.ComponentModel.DataAnnotations;

namespace SignalRLearn.Contracts.Orders;

public sealed class CreateOrderRequest
{
    [Required, StringLength(80, MinimumLength = 2)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 2)]
    public string ProductName { get; set; } = string.Empty;

    [Range(1, 1000)]
    public int Quantity { get; set; } = 1;
}
