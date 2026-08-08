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

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
