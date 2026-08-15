using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using SignalRLearn.Contracts.Orders;

namespace SignalRLearn.Api.Tests;

public sealed class OrdersApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly HttpClient _client;

    public OrdersApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task GetAll_returns_all_orders_sorted_by_updated_date_descending()
    {
        var response = await _client.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orders = await response.Content.ReadFromJsonAsync<IReadOnlyList<OrderDto>>(JsonOptions);
        Assert.NotNull(orders);
        Assert.True(orders.Count >= 4, $"Expected at least 4 seed orders, but got {orders.Count}");

        // Verify orders are sorted by UpdatedAtUtc descending
        for (int i = 0; i < orders.Count - 1; i++)
        {
            Assert.True(orders[i].UpdatedAtUtc >= orders[i + 1].UpdatedAtUtc,
                $"Order at index {i} (UpdatedAtUtc: {orders[i].UpdatedAtUtc}) should have UpdatedAtUtc >= order at index {i + 1} (UpdatedAtUtc: {orders[i + 1].UpdatedAtUtc})");
        }

        // Verify all statuses are represented (seeded orders have one of each status)
        var statuses = orders.Select(o => o.Status).Distinct().ToList();
        Assert.Contains(OrderStatus.Processing, statuses);
        Assert.Contains(OrderStatus.Shipped, statuses);
        Assert.Contains(OrderStatus.Delivered, statuses);
        Assert.Contains(OrderStatus.Cancelled, statuses);
    }

    [Fact]
    public async Task Create_with_empty_customer_name_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "", // Empty - should fail validation
            ProductName = "Test Product",
            Quantity = 1
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("CustomerName", body);
    }

    [Fact]
    public async Task Create_with_too_short_customer_name_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "A", // Too short (min length is 2)
            ProductName = "Test Product",
            Quantity = 1
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("CustomerName", body);
        Assert.Contains("minimum length", body);
    }

    [Fact]
    public async Task Create_with_too_long_customer_name_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = new string('A', 81), // Too long (max length is 80)
            ProductName = "Test Product",
            Quantity = 1
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("CustomerName", body);
        Assert.Contains("maximum length", body);
    }

    [Fact]
    public async Task Create_with_too_short_product_name_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "Test Customer",
            ProductName = "A", // Too short (min length is 2)
            Quantity = 1
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ProductName", body);
        Assert.Contains("minimum length", body);
    }

    [Fact]
    public async Task Create_with_too_long_product_name_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "Test Customer",
            ProductName = new string('A', 121), // Too long (max length is 120)
            Quantity = 1
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ProductName", body);
        Assert.Contains("maximum length", body);
    }

    [Fact]
    public async Task Create_with_zero_quantity_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "Test Customer",
            ProductName = "Test Product",
            Quantity = 0 // Invalid (min is 1)
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Quantity", body, StringComparison.OrdinalIgnoreCase);
        // Check for validation error indicators
        Assert.Contains("errors", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_with_negative_quantity_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "Test Customer",
            ProductName = "Test Product",
            Quantity = -1 // Invalid (min is 1)
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Quantity", body, StringComparison.OrdinalIgnoreCase);
        // Check for validation error indicators
        Assert.Contains("errors", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_with_max_quantity_succeeds()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "Test Customer",
            ProductName = "Test Product",
            Quantity = 1000 // Max valid quantity
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(1000, created.Quantity);
    }

    [Fact]
    public async Task Create_with_min_quantity_succeeds()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "Test Customer",
            ProductName = "Test Product",
            Quantity = 1 // Min valid quantity
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(1, created.Quantity);
    }

    [Fact]
    public async Task Create_trims_customer_and_product_names()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "  John Doe  ",
            ProductName = "  Test Product  ",
            Quantity = 1
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("John Doe", created.CustomerName);
        Assert.Equal("Test Product", created.ProductName);
    }

    [Fact]
    public async Task Create_returns_201_and_the_order_can_be_retrieved()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "Ada Lovelace",
            ProductName = "Analytical Engine notes",
            Quantity = 1
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(OrderStatus.Processing, created.Status);
        Assert.NotNull(response.Headers.Location);

        var retrieved = await _client.GetFromJsonAsync<OrderDto>(response.Headers.Location, JsonOptions);
        Assert.Equal(created, retrieved);
    }

    [Fact]
    public async Task GetById_returns_200_for_existing_order()
    {
        // First, get an existing order ID from the seed data
        var allResponse = await _client.GetAsync("/api/orders");
        var allOrders = await allResponse.Content.ReadFromJsonAsync<IReadOnlyList<OrderDto>>(JsonOptions);
        var existingOrder = allOrders![0]; // Get first order

        var response = await _client.GetAsync($"/api/orders/{existingOrder.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        Assert.NotNull(order);
        Assert.Equal(existingOrder.Id, order.Id);
        Assert.Equal(existingOrder.CustomerName, order.CustomerName);
        Assert.Equal(existingOrder.ProductName, order.ProductName);
        Assert.Equal(existingOrder.Quantity, order.Quantity);
        Assert.Equal(existingOrder.Status, order.Status);
    }

    [Fact]
    public async Task GetById_returns_404_for_non_existent_order()
    {
        var nonExistentId = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/orders/{nonExistentId}");
        
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_status_changes_a_processing_order_to_shipped()
    {
        var created = await CreateOrderAsync();

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/orders/{created.Id}/status")
        {
            Content = JsonContent.Create(
                new UpdateOrderStatusRequest { Status = OrderStatus.Shipped },
                options: JsonOptions)
        };
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        Assert.Equal(OrderStatus.Shipped, updated!.Status);
    }

    [Fact]
    public async Task Invalid_status_transition_returns_validation_problem()
    {
        var created = await CreateOrderAsync();

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/orders/{created.Id}/status")
        {
            Content = JsonContent.Create(
                new UpdateOrderStatusRequest { Status = OrderStatus.Delivered },
                options: JsonOptions)
        };
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Processing to Delivered", body);
    }

    [Fact]
    public async Task Update_status_from_processing_to_cancelled_succeeds()
    {
        var created = await CreateOrderAsync();

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/orders/{created.Id}/status")
        {
            Content = JsonContent.Create(
                new UpdateOrderStatusRequest { Status = OrderStatus.Cancelled },
                options: JsonOptions)
        };
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        Assert.Equal(OrderStatus.Cancelled, updated!.Status);
    }

    [Fact]
    public async Task Update_status_from_shipped_to_delivered_succeeds()
    {
        // First create an order and ship it
        var created = await CreateOrderAsync();
        await UpdateOrderStatusAsync(created.Id, OrderStatus.Shipped);

        // Now try to deliver it
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/orders/{created.Id}/status")
        {
            Content = JsonContent.Create(
                new UpdateOrderStatusRequest { Status = OrderStatus.Delivered },
                options: JsonOptions)
        };
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        Assert.Equal(OrderStatus.Delivered, updated!.Status);
    }

    [Fact]
    public async Task Update_status_from_shipped_to_cancelled_succeeds()
    {
        // First create an order and ship it
        var created = await CreateOrderAsync();
        await UpdateOrderStatusAsync(created.Id, OrderStatus.Shipped);

        // Now try to cancel it
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/orders/{created.Id}/status")
        {
            Content = JsonContent.Create(
                new UpdateOrderStatusRequest { Status = OrderStatus.Cancelled },
                options: JsonOptions)
        };
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        Assert.Equal(OrderStatus.Cancelled, updated!.Status);
    }

    [Fact]
    public async Task Update_status_from_delivered_to_any_status_fails()
    {
        // Find a delivered order from seed data
        var allResponse = await _client.GetAsync("/api/orders");
        var allOrders = await allResponse.Content.ReadFromJsonAsync<IReadOnlyList<OrderDto>>(JsonOptions);
        var deliveredOrder = allOrders!.First(o => o.Status == OrderStatus.Delivered);

        // Try to change it back to Processing (invalid)
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/orders/{deliveredOrder.Id}/status")
        {
            Content = JsonContent.Create(
                new UpdateOrderStatusRequest { Status = OrderStatus.Processing },
                options: JsonOptions)
        };
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Delivered to Processing", body);
    }

    [Fact]
    public async Task Update_status_from_cancelled_to_any_status_fails()
    {
        // Find a cancelled order from seed data
        var allResponse = await _client.GetAsync("/api/orders");
        var allOrders = await allResponse.Content.ReadFromJsonAsync<IReadOnlyList<OrderDto>>(JsonOptions);
        var cancelledOrder = allOrders!.First(o => o.Status == OrderStatus.Cancelled);

        // Try to change it to Shipped (invalid)
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/orders/{cancelledOrder.Id}/status")
        {
            Content = JsonContent.Create(
                new UpdateOrderStatusRequest { Status = OrderStatus.Shipped },
                options: JsonOptions)
        };
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Cancelled to Shipped", body);
    }

    [Fact]
    public async Task Update_status_with_invalid_enum_value_returns_400()
    {
        var created = await CreateOrderAsync();

        // Create invalid status request with an out-of-range enum value
        var invalidRequest = new { Status = 999 }; // Invalid enum value
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/orders/{created.Id}/status")
        {
            Content = JsonContent.Create(invalidRequest, options: JsonOptions)
        };
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Status", body);
    }

    [Fact]
    public async Task Update_status_to_same_status_returns_200_with_unchanged_order()
    {
        var created = await CreateOrderAsync();

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/orders/{created.Id}/status")
        {
            Content = JsonContent.Create(
                new UpdateOrderStatusRequest { Status = OrderStatus.Processing }, // Same as current
                options: JsonOptions)
        };
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        Assert.Equal(OrderStatus.Processing, updated!.Status);
        Assert.Equal(created.Id, updated.Id);
        // Order should remain unchanged
    }

    [Fact]
    public async Task Update_status_for_non_existent_order_returns_404()
    {
        var nonExistentId = Guid.NewGuid();

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/orders/{nonExistentId}/status")
        {
            Content = JsonContent.Create(
                new UpdateOrderStatusRequest { Status = OrderStatus.Shipped },
                options: JsonOptions)
        };
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Updated_order_has_new_updated_timestamp()
    {
        var created = await CreateOrderAsync();
        
        // Get the order first to see original timestamp
        var originalOrderResponse = await _client.GetAsync($"/api/orders/{created.Id}");
        var originalOrder = await originalOrderResponse.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        var originalUpdatedAt = originalOrder!.UpdatedAtUtc;

        // Wait a bit to ensure timestamp difference
        await Task.Delay(10);

        // Update the status
        var updatedOrder = await UpdateOrderStatusAsync(created.Id, OrderStatus.Shipped);

        // Verify the updated timestamp changed
        Assert.True(updatedOrder.UpdatedAtUtc > originalUpdatedAt, 
            $"Updated timestamp {updatedOrder.UpdatedAtUtc} should be greater than original {originalUpdatedAt}");
        
        // Created timestamp should remain unchanged
        Assert.Equal(originalOrder.CreatedAtUtc, updatedOrder.CreatedAtUtc);
    }

    [Fact]
    public async Task OrderDto_records_have_value_equality()
    {
        var created = await CreateOrderAsync();
        
        // Retrieve the same order twice
        var response1 = await _client.GetAsync($"/api/orders/{created.Id}");
        var order1 = await response1.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        
        var response2 = await _client.GetAsync($"/api/orders/{created.Id}");
        var order2 = await response2.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);

        // As records, they should be equal even though they're different instances
        Assert.Equal(order1, order2);
        Assert.True(order1 == order2);
        Assert.False(order1 != order2);
        
        // Hash codes should match for equal records
        Assert.Equal(order1!.GetHashCode(), order2!.GetHashCode());
    }

    [Fact]
    public async Task OrderDto_records_can_be_compared_for_inequality()
    {
        // Create two different orders
        var response1 = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "Customer A",
            ProductName = "Product A",
            Quantity = 1
        }, JsonOptions);
        var order1 = await response1.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        
        var response2 = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "Customer B",
            ProductName = "Product B",
            Quantity = 2
        }, JsonOptions);
        var order2 = await response2.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);

        // Different orders should not be equal
        Assert.NotEqual(order1, order2);
        Assert.True(order1 != order2);
        Assert.False(order1 == order2);
        
        // With high probability, hash codes should differ (not guaranteed but likely)
        // Note: We don't assert on hash codes since different values could theoretically have same hash
    }

    [Fact]
    public async Task OrderDto_with_different_status_is_not_equal()
    {
        var created = await CreateOrderAsync();
        
        // Get original order
        var originalResponse = await _client.GetAsync($"/api/orders/{created.Id}");
        var originalOrder = await originalResponse.Content.ReadFromJsonAsync<OrderDto>(JsonOptions);
        
        // Update status
        var updatedOrder = await UpdateOrderStatusAsync(created.Id, OrderStatus.Shipped);

        // Orders with different status should not be equal
        Assert.NotEqual(originalOrder, updatedOrder);
        Assert.True(originalOrder != updatedOrder);
    }

    private async Task<OrderDto> CreateOrderAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            CustomerName = "Integration test",
            ProductName = "Test package",
            Quantity = 1
        }, JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions))!;
    }

    private async Task<OrderDto> UpdateOrderStatusAsync(Guid orderId, OrderStatus newStatus)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/orders/{orderId}/status")
        {
            Content = JsonContent.Create(
                new UpdateOrderStatusRequest { Status = newStatus },
                options: JsonOptions)
        };
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions))!;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
