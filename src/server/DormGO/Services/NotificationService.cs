using DormGO.Data;
using DormGO.DTOs.ResponseDTO;
using DormGO.Hubs;
using DormGO.Models;
using Mapster;
using Microsoft.AspNetCore.SignalR;

namespace DormGO.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationContext _db;
    private readonly IHubContext<PostHub> _postHub;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ApplicationContext db, IHubContext<PostHub> postHub, ILogger<NotificationService> logger)
    {
        _db = db;
        _postHub = postHub;
        _logger = logger;
    }

    public async Task SendPostNotificationAsync(ApplicationUser user, PostNotification postNotification, string eventName)
    {
        postNotification.UserId = user.Id;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Notification saved successfully. UserId: {UserId}, NotificationId: {NotificationId}", user.Id, postNotification.Id);
        var responseDto = postNotification.Adapt<PostNotificationResponseDto>();
        await _postHub.Clients.User(user.Id).SendAsync(eventName, responseDto);
    }
}