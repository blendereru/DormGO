using System.Security.Claims;
using DormGO.Data;
using DormGO.DTOs;
using DormGO.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DormGO.Hubs;
[Authorize]
public class PostHub : Hub
{
    private readonly ApplicationContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public PostHub(ApplicationContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            var userId = Context?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                Log.Warning("Connection aborted: Missing or empty user ID. ConnectionId: {ConnectionId}", Context.ConnectionId);
                Context.Abort();
                return;
            }
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            var ip = Context?.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrEmpty(ip))
            {
                Log.Warning("Connection aborted: Missing IP address. UserId: {UserId}, ConnectionId: {ConnectionId}", userId, Context.ConnectionId);
                Context.Abort();
                return;
            }
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                Log.Warning("Connection aborted: User not found in database. UserId: {UserId}, ConnectionId: {ConnectionId}", userId, Context.ConnectionId);
                Context.Abort();
                return;
            }
            var connection = new UserConnection
            {
                ConnectionId = Context.ConnectionId,
                UserId = userId,
                Ip = ip!,
                Hub = "/api/posthub",
                ConnectedAt = DateTime.UtcNow
            };
            _db.UserConnections.Add(connection);
            await _db.SaveChangesAsync();
            Log.Information("User connected: UserId: {UserId}, IP: {IPAddress}, ConnectionId: {ConnectionId}", userId, ip, Context.ConnectionId);
            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred during OnConnectedAsync. ConnectionId: {ConnectionId}", Context.ConnectionId);
            Context.Abort();
        }
    }
    public async Task NotifyPostCreated(string userId, PostDto post)
    {
        if (string.IsNullOrEmpty(userId))
        {
            Log.Warning("NotifyPostCreated: userId is null or empty. Skipping notification.");
            return;
        }
        try
        {
            await Clients.Group(userId).SendAsync("PostCreated", true, post);
            await Clients.AllExcept(Context.ConnectionId).SendAsync("PostCreated", false, post);
            Log.Information("PostCreated notification sent. UserId: {UserId}, PostId: {PostId}", userId, post.PostId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred while notifying post creation. UserId: {UserId}, PostId: {PostId}", userId, post.PostId);
        }
    }
    public async Task NotifyPostUpdated(PostDto post)
    {
        try
        {
            await Clients.All.SendAsync("PostUpdated", post);
            Log.Information("PostUpdated notification sent for PostId: {PostId}", post.PostId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred while notifying post update. PostId: {PostId}", post.PostId);
        }
    }
    public async Task NotifyPostJoined(PostDto post)
    {
        try
        {
            await Clients.All.SendAsync("PostJoined", post);
            Log.Information("PostJoined notification sent for PostId: {PostId}", post.PostId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error notifying PostJoined for PostId: {PostId}", post.PostId);
        }
    }

    public async Task NotifyPostUnjoined(PostDto post)
    {
        try
        {
            await Clients.All.SendAsync("PostUnjoined", post);
            Log.Information("PostUnjoined notification sent for PostId: {PostId}", post.PostId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error notifying PostUnjoined for PostId: {PostId}", post.PostId);
        }
    }
    public async Task NotifyPostDeleted(string postId)
    {
        try
        {
            await Clients.All.SendAsync("PostDeleted", postId);
            Log.Information("PostDeleted notification sent for PostId: {PostId}", postId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred while notifying post deletion. PostId: {PostId}", postId);
        }
    }

    public async Task SendNotification(string userId, NotificationDto notification)
    {
        try
        {
            await Clients.User(userId).SendAsync("ReceiveNotification", notification);
            Log.Information("Notification sent to UserId: {UserId}", userId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred while sending notification to user {UserId}", userId);
        }
    }
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var connectionId = Context.ConnectionId;
            var userId = Context?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(connectionId, userId);
            }
            var userConnection = await _db.UserConnections.FirstOrDefaultAsync(uc => uc.ConnectionId == connectionId);
            if (userConnection != null)
            {
                _db.UserConnections.Remove(userConnection);
                await _db.SaveChangesAsync();
                Log.Information("User disconnected: UserId: {UserId}, ConnectionId: {ConnectionId}", userConnection.UserId, connectionId);
            }
            else
            {
                Log.Warning("Connection not found in the database during disconnection. ConnectionId: {ConnectionId}", connectionId);
            }
            if (exception != null)
            {
                Log.Error(exception, "An error occurred during disconnection. ConnectionId: {ConnectionId}", connectionId);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred while processing OnDisconnectedAsync. ConnectionId: {ConnectionId}", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}
