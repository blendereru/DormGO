using DormGO.Data;
using DormGO.DTOs.RequestDTO;
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
    public PostNotificationService(ApplicationContext db, IHubContext<PostHub> hub)
    {
        _db = db;
        _hub = hub;
    }
    public async Task NotifyUserAsync(string userId, NotificationResponseDto notificationDto)
    {
        var notification = notificationDto.Adapt<PostNotification>();
        notification.UserId = userId;
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();
        var updatedNotificationDto = notification.Adapt<NotificationResponseDto>();
        await _hub.Clients.User(userId).SendAsync("ReceiveNotification", updatedNotificationDto);
    }
}