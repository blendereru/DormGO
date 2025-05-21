using DormGO.Data;
using DormGO.DTOs.ResponseDTO;
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

    public async Task NotifyMessageSentAsync(ApplicationUser user, MessageResponseDto messageResponseDto)
    {
        var notificationDto = new
        {
            messageResponseDto.MessageId,
            messageResponseDto.Content,
            SenderName = messageResponseDto.Sender.Name,
            messageResponseDto.SentAt,
            messageResponseDto.UpdatedAt
        };

        var excludedConnectionIds = await _db.UserConnections
            .Where(c => c.UserId == user.Id && c.Hub == "/api/chathub")
            .Select(uc => uc.ConnectionId)
            .ToListAsync();

        await _hub.Clients.GroupExcept(messageResponseDto.Post.PostId, excludedConnectionIds)
            .SendAsync("MessageSent", notificationDto);

        _logger.LogInformation(
            "Message sent notification sent. PostId: {PostId}, MessageId: {MessageId}, ExcludedConnectionsCount: {ExcludedConnectionsCount}",
            messageResponseDto.Post.PostId, messageResponseDto.MessageId, excludedConnectionIds.Count);
    }

    public async Task NotifyMessageUpdatedAsync(ApplicationUser user, MessageResponseDto messageResponseDto)
    {
        var notificationDto = new
        {
            messageResponseDto.MessageId,
            messageResponseDto.Content,
            SenderName = messageResponseDto.Sender.Name,
            messageResponseDto.SentAt,
            messageResponseDto.UpdatedAt
        };
        var excludedConnectionIds = await _db.UserConnections
            .Where(c => c.UserId == user.Id && c.Hub == "/api/chathub")
            .Select(uc => uc.ConnectionId)
            .ToListAsync();
        await _hub.Clients.GroupExcept(messageResponseDto.Post.PostId, excludedConnectionIds)
            .SendAsync("MessageUpdated", notificationDto);

        _logger.LogInformation("Message updated notification sent. PostId: {PostId}, MessageId: {MessageId}, ExcludedConnectionsCount: {ExcludedConnectionsCount}",
            messageResponseDto.Post.PostId, messageResponseDto.MessageId, excludedConnectionIds.Count);
    }

    public async Task NotifyMessageDeletedAsync(ApplicationUser user, MessageResponseDto messageResponseDto)
    {
        var notificationDto = new
        {
            messageResponseDto.MessageId,
            SenderName = messageResponseDto.Sender.Name
        };
        var excludedConnectionIds = await _db.UserConnections
            .Where(c => c.UserId == user.Id && c.Hub == "/api/chathub")
            .Select(uc => uc.ConnectionId)
            .ToListAsync();
        await _hub.Clients.GroupExcept(messageResponseDto.Post.PostId, excludedConnectionIds)
            .SendAsync("MessageDeleted", notificationDto);
        _logger.LogInformation(
            "Message deleted notification sent. PostId: {PostId}, MessageId: {MessageId}, ExcludedConnectionsCount: {ExcludedConnectionsCount}",
            messageResponseDto.Post.PostId, messageResponseDto.MessageId, excludedConnectionIds.Count);
    }
}