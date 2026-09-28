using System.Windows;
using Microsoft.AspNetCore.SignalR.Client;
using ServiceDesk.Contracts;

namespace ServiceDesk.Desktop.Services;

// Подписка на событие OrderUpdated хаба /hubs/orders; обработчики вызываются в потоке интерфейса
public static class RealtimeClient
{
    private static HubConnection _connection;

    public static event Action<OrderUpdatedEvent> OrderUpdated;

    public static async Task StartAsync()
    {
        await StopAsync();
        _connection = new HubConnectionBuilder()
            .WithUrl(ApiClient.BaseUrl + "/hubs/orders", o => o.AccessTokenProvider = () => Task.FromResult(Session.Token))
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
            .Build();
        _connection.On<OrderUpdatedEvent>("OrderUpdated", e =>
            Application.Current?.Dispatcher.BeginInvoke(() => OrderUpdated?.Invoke(e)));
        try
        {
            await _connection.StartAsync();
        }
        catch
        {
            // уведомления не критичны: страницы можно обновить вручную
        }
    }

    public static async Task StopAsync()
    {
        if (_connection == null) return;
        var c = _connection;
        _connection = null;
        try { await c.DisposeAsync(); } catch { /* */ }
    }
}
