using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using SignalRLearn.Contracts.Orders;
using SignalRLearn.Contracts.Realtime;

namespace SignalRLearn.Client.Services;

public enum RealtimeStatus
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting
}

public sealed class OrderRealtimeClient : IAsyncDisposable
{
    private readonly OrderState _orderState;
    private readonly OrderApiClient _orderApiClient;
    private readonly HubConnection _connection;
    private readonly SemaphoreSlim _startGate = new(1, 1);

    public OrderRealtimeClient(
        ApiSettings settings,
        OrderState orderState,
        OrderApiClient orderApiClient)
    {
        _orderState = orderState;
        _orderApiClient = orderApiClient;
        _connection = new HubConnectionBuilder()
            .WithUrl(new Uri(settings.BaseAddress, "hubs/orders"))
            .WithAutomaticReconnect([
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10)
            ])
            .AddJsonProtocol(options =>
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();

        _connection.On<OrderDto>(nameof(IOrderClient.OrderCreated), order =>
        {
            _orderState.Upsert(order);
            return Task.CompletedTask;
        });
        _connection.On<OrderStatusChangedEvent>(nameof(IOrderClient.OrderStatusChanged), update =>
        {
            _orderState.Upsert(update.Order);
            return Task.CompletedTask;
        });
        _connection.On<OrderQuantityChangedEvent>(nameof(IOrderClient.OrderQuantityChanged), update =>
        {
            _orderState.Upsert(update.Order);
            QuantityChanged?.Invoke(update);
            return Task.CompletedTask;
        });

        _connection.Reconnecting += _ =>
        {
            SetStatus(RealtimeStatus.Reconnecting);
            return Task.CompletedTask;
        };
        _connection.Reconnected += async _ =>
        {
            SetStatus(RealtimeStatus.Connected);
            await RefreshSnapshotAsync();
        };
        _connection.Closed += _ =>
        {
            SetStatus(RealtimeStatus.Disconnected);
            return Task.CompletedTask;
        };
    }

    public event Action? StatusChanged;

    public event Action<OrderQuantityChangedEvent>? QuantityChanged;

    public RealtimeStatus Status { get; private set; } = RealtimeStatus.Disconnected;

    public async Task EnsureStartedAsync(CancellationToken cancellationToken = default)
    {
        if (_connection.State is HubConnectionState.Connected or HubConnectionState.Connecting or HubConnectionState.Reconnecting)
        {
            return;
        }

        await _startGate.WaitAsync(cancellationToken);
        try
        {
            if (_connection.State != HubConnectionState.Disconnected)
            {
                return;
            }

            SetStatus(RealtimeStatus.Connecting);
            try
            {
                await _connection.StartAsync(cancellationToken);
                SetStatus(RealtimeStatus.Connected);
            }
            catch
            {
                SetStatus(RealtimeStatus.Disconnected);
                throw;
            }
        }
        finally
        {
            _startGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _startGate.Dispose();
        await _connection.DisposeAsync();
    }

    private async Task RefreshSnapshotAsync()
    {
        try
        {
            _orderState.MergeSnapshot(await _orderApiClient.GetOrdersAsync());
        }
        catch
        {
            // A page-level refresh can surface API errors. Reconnection itself
            // remains successful even if this best-effort resync fails.
        }
    }

    private void SetStatus(RealtimeStatus status)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;
        StatusChanged?.Invoke();
    }
}
