using DormGO.Data;
using DormGO.DTOs.ResponseDTO;
using DormGO.Hubs;
using DormGO.Models;
using Mapster;
using Microsoft.AspNetCore.SignalR;

namespace DormGO.Services;

public class PostNotificationService : INotificationService
{
    private readonly ApplicationContext _db;
    private readonly IHubContext<PostHub> _hub;
    private readonly ILogger<PostNotificationService> _logger;
    public PostNotificationService(ApplicationContext db, IHubContext<PostHub> hub, ILogger<PostNotificationService> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }
    public async Task NotifyUserAsync(string userId, NotificationResponseDto notificationDto)
    {
        var notification = notificationDto.Adapt<PostNotification>();
        notification.UserId = userId;
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Notification saved to database. NotificationId: {NotificationId}", notification.Id);
        var updatedNotificationDto = notification.Adapt<NotificationResponseDto>();
        await _hub.Clients.User(userId).SendAsync("ReceiveNotification", updatedNotificationDto);
        _logger.LogInformation("Message sent to hub on notification. UserId: {UserId}, NotificationId: {NotificationId}", userId, notification.Id);
    }
}