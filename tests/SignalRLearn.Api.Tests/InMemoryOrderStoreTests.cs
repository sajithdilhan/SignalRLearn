using SignalRLearn.Api.Services;
using SignalRLearn.Contracts.Orders;

namespace SignalRLearn.Api.Tests;

public sealed class InMemoryOrderStoreTests
{
    [Fact]
    public void Starts_with_one_demo_order_in_each_status()
    {
        var store = CreateStore();

        var orders = store.GetAll();

        Assert.Equal(4, orders.Count);
        Assert.All(Enum.GetValues<OrderStatus>(), status =>
            Assert.Contains(orders, order => order.Status == status));
    }

    [Fact]
    public void Create_trims_input_and_starts_in_processing()
    {
        var store = CreateStore();

        var created = store.Create(new CreateOrderRequest
        {
            CustomerName = "  Grace Hopper  ",
            ProductName = "  Compiler manual  ",
            Quantity = 2
        });

        Assert.Equal("Grace Hopper", created.CustomerName);
        Assert.Equal("Compiler manual", created.ProductName);
        Assert.Equal(2, created.Quantity);
        Assert.Equal(OrderStatus.Processing, created.Status);
        Assert.Equal(created, store.GetById(created.Id));
    }

    [Fact]
    public void Valid_status_transition_updates_the_order()
    {
        var store = CreateStore();
        var created = CreateOrder(store);

        var result = store.UpdateStatus(created.Id, OrderStatus.Shipped);

        Assert.Equal(UpdateOrderOutcome.Updated, result.Outcome);
        Assert.Equal(OrderStatus.Processing, result.PreviousStatus);
        Assert.Equal(OrderStatus.Shipped, result.Order!.Status);
    }

    [Fact]
    public void Invalid_status_transition_preserves_the_order()
    {
        var store = CreateStore();
        var created = CreateOrder(store);

        var result = store.UpdateStatus(created.Id, OrderStatus.Delivered);

        Assert.Equal(UpdateOrderOutcome.InvalidTransition, result.Outcome);
        Assert.Equal(OrderStatus.Processing, store.GetById(created.Id)!.Status);
        Assert.Contains("Processing to Delivered", result.Error);
    }

    private static InMemoryOrderStore CreateStore() => new(TimeProvider.System);

    private static OrderDto CreateOrder(IOrderStore store) => store.Create(new CreateOrderRequest
    {
        CustomerName = "Test customer",
        ProductName = "Test product",
        Quantity = 1
    });
}
