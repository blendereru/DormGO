using DormGO.Data;
using DormGO.DTOs.ResponseDTO;
using DormGO.Hubs;
using DormGO.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DormGO.Services.HubNotifications;

public class PostHubNotificationService : IPostHubNotificationService
{
    private readonly ApplicationContext _db;
    private readonly IHubContext<PostHub> _hub;
    private readonly ILogger<PostHubNotificationService> _logger;

    public PostHubNotificationService(ApplicationContext db, IHubContext<PostHub> hub,
        ILogger<PostHubNotificationService> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    public async Task NotifyPostCreatedAsync(ApplicationUser user, Post post)
    {
        var notificationDto = new
        {
            post.Id,
            post.Title,
            post.Description,
            post.CreatedAt,
            CreatorName = post.Creator.UserName,
            post.MaxPeople
        };
        var excludedConnectionIds = await _db.UserConnections
            .Where(c => c.UserId == user.Id && c.Hub == "/api/posthub")
            .Select(uc => uc.ConnectionId)
            .ToListAsync();
        await _hub.Clients.AllExcept(excludedConnectionIds).SendAsync("PostCreated", notificationDto);
        _logger.LogInformation(
            "Post created notification sent. UserId: {UserId}, PostId: {PostId}, ExcludedConnectionsCount: {ExcludedConnectionsCount}",
            user.Id, post.Id, excludedConnectionIds.Count);
    }
    public async Task NotifyPostUpdatedAsync(ApplicationUser user, Post post)
    {
        var notificationDto = new
        {
            post.Id,
            post.Title,
            post.Description,
            post.UpdatedAt,
            CreatorName = post.Creator.UserName,
            post.MaxPeople
        };
        var excludedConnectionIds = await _db.UserConnections
            .Where(c => c.UserId == user.Id && c.Hub == "/api/posthub")
            .Select(uc => uc.ConnectionId)
            .ToListAsync();
        await _hub.Clients.AllExcept(excludedConnectionIds).SendAsync("PostUpdated", notificationDto);
        _logger.LogInformation(
            "Post updated notification sent. UserId: {UserId}, PostId: {PostId}, ExcludedConnectionsCount: {ExcludedConnectionsCount}",
            user.Id, post.Id, excludedConnectionIds.Count);
    }

    public async Task NotifyPostDeletedAsync(ApplicationUser user, string postId)
    {
        var notificationDto = new
        {
            Id = postId
        };
        var excludedConnectionIds = await _db.UserConnections
            .Where(c => c.UserId == user.Id && c.Hub == "/api/posthub")
            .Select(uc => uc.ConnectionId)
            .ToListAsync();
        await _hub.Clients.AllExcept(excludedConnectionIds).SendAsync("PostDeleted", notificationDto);
        _logger.LogInformation(
            "Post deleted notification sent. UserId: {UserId}, PostId: {PostId}, ExcludedConnectionsCount: {ExcludedConnectionsCount}",
            user.Id, postId, excludedConnectionIds.Count);
    }

    public async Task NotifyPostJoinedAsync(ApplicationUser user, string postId)
    {
        var notificationDto = new
        {
            UserId = user.Id,
            user.UserName,
            postId
        };
        var excludedConnectionIds = await _db.UserConnections
            .Where(c => c.UserId == user.Id && c.Hub == "/api/posthub")
            .Select(uc => uc.ConnectionId)
            .ToListAsync();
        await _hub.Clients.AllExcept(excludedConnectionIds)
            .SendAsync("PostJoined", notificationDto);
        _logger.LogInformation(
            "Post joined notification sent. PostId: {PostId}, UserId: {UserId}, ExcludedConnectionsCount: {ExcludedConnectionsCount}",
            postId, user.Id, excludedConnectionIds.Count);
    }

    public async Task NotifyPostLeftAsync(ApplicationUser user, string postId)
    {
        var notificationDto = new
        {
            UserId = user.Id,
            user.UserName,
            postId
        };
        var excludedConnectionIds = await _db.UserConnections
            .Where(c => c.UserId == user.Id && c.Hub == "/api/posthub")
            .Select(uc => uc.ConnectionId)
            .ToListAsync();
        await _hub.Clients.AllExcept(excludedConnectionIds)
            .SendAsync("PostLeft", notificationDto);
        _logger.LogInformation(
            "Post left notification sent. PostId: {PostId}, UserId: {UserId}, ExcludedConnectionsCount: {ExcludedConnectionsCount}",
            postId, user.Id, excludedConnectionIds.Count);
    }
}