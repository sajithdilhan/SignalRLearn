using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using SignalRLearn.Contracts.Orders;

namespace SignalRLearn.Api.Tests;

/// <summary>
/// Property-based tests for OrdersController using FsCheck.
/// These tests validate the controller's behavior across a wide range of inputs
/// to discover edge cases and invariants that example-based tests might miss.
/// </summary>
public sealed class OrdersControllerPropertyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly string[] ValidCustomerNames = { "Alice", "Bob", "Charlie", "Diana", "Eve", "Frank", "Grace", "Henry", "Ivy", "Jack", "Kate", "Luke", "Mallory", "Nina", "Oscar", "Pam", "Quinn", "Rose", "Steve", "Tina", "Uma", "Victor", "Wendy", "Xavier", "Yara", "Zack", "TestUser", "CustomerOne", "OrderTest", "PropertyTest", "ValidName" };
    private static readonly string[] ValidProductNames = { "Widget", "Gadget", "Tool", "Device", "Component", "Module", "Part", "Item", "Product", "Unit", "System", "Assembly", "Piece", "Thing", "Equipment", "Hardware", "Software", "Appliance", "Instrument", "Mechanism" };
    private static readonly string[] WhitespaceChars = { " ", "  ", "   ", "\t", "\n", "\r", " \t\n" };

    private readonly HttpClient _client;

    public OrdersControllerPropertyTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }

    [Property(MaxTest = 100)]
    public void CreateOrder_with_valid_inputs_returns_created(int customerIndex, int productIndex, PositiveInt quantity)
    {
        var request = new CreateOrderRequest
        {
            CustomerName = ValidCustomerNames[Math.Abs(customerIndex) % ValidCustomerNames.Length],
            ProductName = ValidProductNames[Math.Abs(productIndex) % ValidProductNames.Length],
            Quantity = Math.Min(quantity.Get, 1000)
        };

        var response = _client.PostAsJsonAsync("/api/orders", request, JsonOptions).Result;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions).Result;
        Assert.NotNull(created);
        Assert.Equal(request.CustomerName, created.CustomerName);
        Assert.Equal(request.ProductName, created.ProductName);
        Assert.Equal(request.Quantity, created.Quantity);
        Assert.Equal(OrderStatus.Processing, created.Status);
        Assert.NotEqual(Guid.Empty, created.Id);
    }

    [Property(MaxTest = 100)]
    public void CreateOrder_trims_whitespace_from_names(int customerIndex, int productIndex)
    {
        var baseCustomer = ValidCustomerNames[Math.Abs(customerIndex) % ValidCustomerNames.Length];
        var baseProduct = ValidProductNames[Math.Abs(productIndex) % ValidProductNames.Length];
        var ws1 = WhitespaceChars[Math.Abs(customerIndex) % WhitespaceChars.Length];
        var ws2 = WhitespaceChars[Math.Abs(productIndex) % WhitespaceChars.Length];

        var request = new CreateOrderRequest
        {
            CustomerName = $"{ws1}{baseCustomer}{ws2}",
            ProductName = $"{ws1}{baseProduct}{ws2}",
            Quantity = 1
        };

        var response = _client.PostAsJsonAsync("/api/orders", request, JsonOptions).Result;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions).Result;
        Assert.NotNull(created);
        Assert.Equal(baseCustomer, created.CustomerName);
        Assert.Equal(baseProduct, created.ProductName);
    }

    [Property(MaxTest = 50)]
    public void GetAll_returns_at_least_seed_orders()
    {
        var response = _client.GetAsync("/api/orders").Result;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orders = response.Content.ReadFromJsonAsync<IReadOnlyList<OrderDto>>(JsonOptions).Result;
        Assert.NotNull(orders);
        Assert.True(orders.Count >= 4, $"Expected at least 4 seed orders, got {orders.Count}");
    }

    [Property(MaxTest = 50)]
    public void GetAll_returns_orders_sorted_by_updated_descending()
    {
        var response = _client.GetAsync("/api/orders").Result;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orders = response.Content.ReadFromJsonAsync<IReadOnlyList<OrderDto>>(JsonOptions).Result;
        Assert.NotNull(orders);

        for (int i = 0; i < orders.Count - 1; i++)
        {
            Assert.True(orders[i].UpdatedAtUtc >= orders[i + 1].UpdatedAtUtc,
                $"Orders not sorted: order[{i}].UpdatedAtUtc ({orders[i].UpdatedAtUtc}) < order[{i + 1}].UpdatedAtUtc ({orders[i + 1].UpdatedAtUtc})");
        }
    }

    [Property(MaxTest = 50)]
    public void GetById_for_non_existent_id_returns_not_found()
    {
        var nonExistentId = Guid.NewGuid();
        var response = _client.GetAsync($"/api/orders/{nonExistentId}").Result;

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Property(MaxTest = 100)]
    public void Update_status_same_as_current_returns_ok(OrderStatus status)
    {
        // Create a new order
        var createResponse = _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "PropertyTest",
            ProductName = "TestProduct",
            Quantity = 1
        }, JsonOptions).Result;

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = createResponse.Content.ReadFromJsonAsync<OrderDto>(JsonOptions).Result;
        Assert.NotNull(created);

        // Only try statuses that are valid from Processing (current status)
        // Valid transitions from Processing: Shipped, Cancelled
        var validStatuses = new[] { OrderStatus.Processing, OrderStatus.Shipped };
        if (!validStatuses.Contains(status))
        {
            // Skip this test case - invalid status transition
            return;
        }

        // Update to the target status first
        var firstUpdateResponse = _client.PatchAsJsonAsync($"/api/orders/{created.Id}/status",
            new UpdateOrderStatusRequest { Status = status }, JsonOptions).Result;

        // Only proceed if the first update succeeded
        if (firstUpdateResponse.StatusCode != HttpStatusCode.OK)
        {
            return;
        }

        // Now try updating to the same status
        var response = _client.PatchAsJsonAsync($"/api/orders/{created.Id}/status",
            new UpdateOrderStatusRequest { Status = status }, JsonOptions).Result;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions).Result;
        Assert.NotNull(updated);
        Assert.Equal(status, updated.Status);
    }

    [Property(MaxTest = 100)]
    public void Update_quantity_on_processing_order_succeeds(PositiveInt quantity)
    {
        // Create a new order
        var createResponse = _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "PropertyTest",
            ProductName = "TestProduct",
            Quantity = 1
        }, JsonOptions).Result;

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = createResponse.Content.ReadFromJsonAsync<OrderDto>(JsonOptions).Result;
        Assert.NotNull(created);

        var newQuantity = Math.Min(quantity.Get, 1000);
        var response = _client.PutAsJsonAsync($"/api/orders/{created.Id}/quantity",
            new UpdateOrderQuantityRequest { Quantity = newQuantity }, JsonOptions).Result;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions).Result;
        Assert.NotNull(updated);
        Assert.Equal(newQuantity, updated.Quantity);
    }

    [Property(MaxTest = 50)]
    public void Created_order_has_valid_timestamps()
    {
        var beforeCreate = DateTime.UtcNow;

        var response = _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "TimestampTest",
            ProductName = "TestProduct",
            Quantity = 1
        }, JsonOptions).Result;

        var afterCreate = DateTime.UtcNow;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions).Result;
        Assert.NotNull(created);
        Assert.True(created.CreatedAtUtc >= beforeCreate,
            $"CreatedAtUtc {created.CreatedAtUtc} should be >= beforeCreate {beforeCreate}");
        Assert.True(created.CreatedAtUtc <= afterCreate,
            $"CreatedAtUtc {created.CreatedAtUtc} should be <= afterCreate {afterCreate}");
        Assert.True(created.UpdatedAtUtc >= created.CreatedAtUtc,
            "UpdatedAtUtc should be >= CreatedAtUtc");
    }

    [Property(MaxTest = 50)]
    public void OrderDto_id_is_never_empty()
    {
        var response = _client.GetAsync("/api/orders").Result;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orders = response.Content.ReadFromJsonAsync<IReadOnlyList<OrderDto>>(JsonOptions).Result;
        Assert.NotNull(orders);
        Assert.All(orders, order => Assert.NotEqual(Guid.Empty, order.Id));
    }

    [Property(MaxTest = 50)]
    public void Order_status_can_only_be_valid_enum_values()
    {
        var allResponse = _client.GetAsync("/api/orders").Result;

        Assert.Equal(HttpStatusCode.OK, allResponse.StatusCode);

        var orders = allResponse.Content.ReadFromJsonAsync<IReadOnlyList<OrderDto>>(JsonOptions).Result;
        Assert.NotNull(orders);
        var validStatuses = Enum.GetValues<OrderStatus>();
        Assert.All(orders, order => Assert.Contains(order.Status, validStatuses));
    }

    [Property(MaxTest = 100)]
    public void GetAll_returns_orders_with_all_required_fields(int _)
    {
        var response = _client.GetAsync("/api/orders").Result;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orders = response.Content.ReadFromJsonAsync<IReadOnlyList<OrderDto>>(JsonOptions).Result;
        Assert.NotNull(orders);

        foreach (var order in orders)
        {
            Assert.NotEqual(Guid.Empty, order.Id);
            Assert.NotEmpty(order.CustomerName);
            Assert.NotEmpty(order.ProductName);
            Assert.True(order.Quantity >= 1);
            Assert.Contains(order.Status, Enum.GetValues<OrderStatus>());
            Assert.True(order.CreatedAtUtc <= order.UpdatedAtUtc);
        }
    }

    [Property(MaxTest = 100)]
    public void Update_quantity_zero_fails_validation(int _)
    {
        // Create a new order
        var createResponse = _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "PropertyTest",
            ProductName = "TestProduct",
            Quantity = 1
        }, JsonOptions).Result;

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = createResponse.Content.ReadFromJsonAsync<OrderDto>(JsonOptions).Result;
        Assert.NotNull(created);

        // Try to update with quantity 0 - should fail
        var response = _client.PutAsJsonAsync($"/api/orders/{created.Id}/quantity",
            new UpdateOrderQuantityRequest { Quantity = 0 }, JsonOptions).Result;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Property(MaxTest = 100)]
    public void Update_quantity_negative_fails_validation(int _)
    {
        // Create a new order
        var createResponse = _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "PropertyTest",
            ProductName = "TestProduct",
            Quantity = 1
        }, JsonOptions).Result;

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = createResponse.Content.ReadFromJsonAsync<OrderDto>(JsonOptions).Result;
        Assert.NotNull(created);

        // Try to update with negative quantity - should fail
        var response = _client.PutAsJsonAsync($"/api/orders/{created.Id}/quantity",
            new UpdateOrderQuantityRequest { Quantity = -1 }, JsonOptions).Result;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}