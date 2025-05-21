using DormGO.Data;
using DormGO.DTOs.ResponseDTO;
using DormGO.Hubs;
using DormGO.Models;
using Mapster;
using Microsoft.AspNetCore.SignalR;

namespace DormGO.Services;

public class NotificationService<TNotification, TResponseDto> : INotificationService<TNotification, TResponseDto>
    where TNotification : Notification
    where TResponseDto : NotificationResponseDto
{
    private readonly ApplicationContext _db;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<NotificationService<TNotification, TResponseDto>> _logger;

    public NotificationService(ApplicationContext db, IHubContext<NotificationHub> hub, ILogger<NotificationService<TNotification, TResponseDto>> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    public async Task SendNotificationAsync(ApplicationUser user, TNotification notification, string eventName)
    {
        notification.UserId = user.Id;
        _db.Set<Notification>().Add(notification);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Notification saved successfully. UserId: {UserId}, NotificationId: {NotificationId}", user.Id, notification.Id);
        var responseDto = notification.Adapt<TResponseDto>();
        await _hub.Clients.User(user.Id).SendAsync(eventName, responseDto);
    }
}