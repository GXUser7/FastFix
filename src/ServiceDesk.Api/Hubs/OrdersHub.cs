using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ServiceDesk.Api.Infrastructure;
using ServiceDesk.Contracts;

namespace ServiceDesk.Api.Hubs;

// Хаб уведомлений /hubs/orders: клиент получает события только по своим заявкам, сотрудники — по всем
[Authorize]
public class OrdersHub : Hub
{
    public const string Event = "OrderUpdated";
    public const string StaffGroup = "staff";
    public static string UserGroup(int userId) => "user:" + userId;

    public override async Task OnConnectedAsync()
    {
        var user = CurrentUser.From(Context.User);
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(user.Id));
        if (user.Role != Roles.Client)
            await Groups.AddToGroupAsync(Context.ConnectionId, StaffGroup);
        await base.OnConnectedAsync();
    }
}

public interface INotifier
{
    Task OrderUpdatedAsync(int clientId, OrderUpdatedEvent e);
}

public class NotificationService : INotifier
{
    private readonly IHubContext<OrdersHub> _hub;
    private readonly ILogger<NotificationService> _log;

    public NotificationService(IHubContext<OrdersHub> hub, ILogger<NotificationService> log)
    {
        _hub = hub;
        _log = log;
    }

    public async Task OrderUpdatedAsync(int clientId, OrderUpdatedEvent e)
    {
        try
        {
            await _hub.Clients.Groups(OrdersHub.UserGroup(clientId), OrdersHub.StaffGroup).SendAsync(OrdersHub.Event, e);
        }
        catch (Exception ex)
        {
            // сбой уведомления не должен отменять уже сохранённую операцию
            _log.LogWarning(ex, "Не удалось отправить уведомление по заявке {Id}", e.OrderId);
        }
    }
}
