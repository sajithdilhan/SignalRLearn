using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SignalRLearn.Contracts.Orders;

namespace SignalRLearn.Client.Services;

public sealed class OrderApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/orders", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<OrderDto[]>(SerializerOptions, cancellationToken) ?? [];
    }

    public async Task<OrderDto?> GetOrderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/orders/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<OrderDto>(SerializerOptions, cancellationToken);
    }

    public async Task<OrderDto> CreateOrderAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/orders", request, SerializerOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<OrderDto>(SerializerOptions, cancellationToken))!;
    }

    public async Task<OrderDto> UpdateStatusAsync(
        Guid id,
        OrderStatus status,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Patch, $"api/orders/{id}/status")
        {
            Content = JsonContent.Create(new UpdateOrderStatusRequest { Status = status }, options: SerializerOptions)
        };
        using var response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<OrderDto>(SerializerOptions, cancellationToken))!;
    }

    public async Task<OrderDto> UpdateQuantityAsync(
        Guid id,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Put, $"api/orders/{id}/quantity")
        {
            Content = JsonContent.Create(new UpdateOrderQuantityRequest { Quantity = quantity }, options: SerializerOptions)
        };
        using var response = await httpClient.SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<OrderDto>(SerializerOptions, cancellationToken))!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var fallback = $"The API returned {(int)response.StatusCode} ({response.ReasonPhrase}).";
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            if (root.TryGetProperty("errors", out var errors))
            {
                var messages = errors.EnumerateObject()
                    .SelectMany(property => property.Value.EnumerateArray())
                    .Select(value => value.GetString())
                    .Where(value => !string.IsNullOrWhiteSpace(value));
                var validationMessage = string.Join(" ", messages!);
                if (!string.IsNullOrWhiteSpace(validationMessage))
                {
                    throw new ApiException(validationMessage, (int)response.StatusCode);
                }
            }

            var message = root.TryGetProperty("detail", out var detail)
                ? detail.GetString()
                : root.TryGetProperty("title", out var title) ? title.GetString() : fallback;
            throw new ApiException(message ?? fallback, (int)response.StatusCode);
        }
        catch (JsonException)
        {
            throw new ApiException(fallback, (int)response.StatusCode);
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
