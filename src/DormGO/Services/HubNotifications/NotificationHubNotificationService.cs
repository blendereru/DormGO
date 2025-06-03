using DormGO.Data;
using DormGO.DTOs.ResponseDTO;
using DormGO.Hubs;
using DormGO.Models;
using Mapster;
using Microsoft.AspNetCore.SignalR;

namespace DormGO.Services.HubNotifications;

public class NotificationHubNotificationService<TNotification> : INotificationHubNotificationService<TNotification>
    where TNotification : Notification
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<NotificationHubNotificationService<TNotification>> _logger;

    public NotificationHubNotificationService(IHubContext<NotificationHub> hub,
        ILogger<NotificationHubNotificationService<TNotification>> logger)
    {
        _hub = hub;
        _logger = logger;
    }
    
    public async Task NotifyNotificationSentAsync(ApplicationUser user, TNotification notification)
    {
        await _hub.Clients.User(user.Id).SendAsync("NotificationSent", notification.Id);
        _logger.LogInformation("Notification sent successfully. UserId: {UserId}, NotificationId: {NotificationId}", user.Id, notification.Id);
    }
}