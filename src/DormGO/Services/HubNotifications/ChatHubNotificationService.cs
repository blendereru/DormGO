using DormGO.Data;
using DormGO.Hubs;
using DormGO.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DormGO.Services.HubNotifications;

public class ChatHubNotificationService : IChatHubNotificationService
{
    private readonly ApplicationContext _db;
    private readonly IHubContext<ChatHub> _hub;
    private readonly ILogger<UserHubNotificationService> _logger;

    public ChatHubNotificationService(ApplicationContext db, IHubContext<ChatHub> hub,
        ILogger<UserHubNotificationService> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    public async Task NotifyMessageSentAsync(ApplicationUser user, Message message)
    {
        var notificationDto = new
        {
            message.Id,
            message.Content,
            SenderName = message.Sender.UserName,
            message.SentAt,
            message.UpdatedAt
        };

        var excludedConnectionIds = await _db.UserConnections
            .Where(c => c.UserId == user.Id && c.Hub == "/api/chathub")
            .Select(uc => uc.ConnectionId)
            .ToListAsync();

        await _hub.Clients.GroupExcept(message.PostId, excludedConnectionIds)
            .SendAsync("MessageSent", notificationDto);

        _logger.LogInformation(
            "Message sent notification sent. PostId: {PostId}, MessageId: {MessageId}, ExcludedConnectionsCount: {ExcludedConnectionsCount}",
            message.PostId, message.Id, excludedConnectionIds.Count);
    }

    public async Task NotifyMessageUpdatedAsync(ApplicationUser user, Message message)
    {
        var notificationDto = new
        {
            message.Id,
            message.Content,
            SenderName = message.Sender.UserName,
            message.SentAt,
            message.UpdatedAt
        };
        var excludedConnectionIds = await _db.UserConnections
            .Where(c => c.UserId == user.Id && c.Hub == "/api/chathub")
            .Select(uc => uc.ConnectionId)
            .ToListAsync();
        await _hub.Clients.GroupExcept(message.PostId, excludedConnectionIds)
            .SendAsync("MessageUpdated", notificationDto);

        _logger.LogInformation("Message updated notification sent. PostId: {PostId}, MessageId: {MessageId}, ExcludedConnectionsCount: {ExcludedConnectionsCount}",
            message.PostId, message.Id, excludedConnectionIds.Count);
    }

    public async Task NotifyMessageDeletedAsync(ApplicationUser user, Message message)
    {
        var notificationDto = new
        {
            message.Id,
            SenderName = message.Sender.UserName
        };
        var excludedConnectionIds = await _db.UserConnections
            .Where(c => c.UserId == user.Id && c.Hub == "/api/chathub")
            .Select(uc => uc.ConnectionId)
            .ToListAsync();
        await _hub.Clients.GroupExcept(message.PostId, excludedConnectionIds)
            .SendAsync("MessageDeleted", notificationDto);
        _logger.LogInformation(
            "Message deleted notification sent. PostId: {PostId}, MessageId: {MessageId}, ExcludedConnectionsCount: {ExcludedConnectionsCount}",
            message.PostId, message.Id, excludedConnectionIds.Count);
    }
}