using System.Security.Claims;
using IdentityApiAuth.Data;
using IdentityApiAuth.DTOs;
using IdentityApiAuth.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace IdentityApiAuth.Hubs;
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
            var userName = Context?.User?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value; 
            if (string.IsNullOrEmpty(userName)) 
            { 
                Log.Warning("Connection aborted: Missing or empty username. ConnectionId: {ConnectionId}", Context.ConnectionId); 
                Context.Abort();
                await Task.CompletedTask;
            } 
            var ip = Context?.GetHttpContext()?.Connection.RemoteIpAddress?.ToString(); 
            if (string.IsNullOrEmpty(ip)) 
            { 
                Log.Warning("Connection aborted: Missing IP address. UserName: {UserName}, ConnectionId: {ConnectionId}", userName, Context.ConnectionId); 
                Context.Abort();
                return;
            } 
            var user = await _userManager.FindByNameAsync(userName!); 
            if (user == null) 
            { 
                Log.Warning("Connection aborted: User not found in database. UserName: {UserName}, ConnectionId: {ConnectionId}", userName, Context.ConnectionId); 
                Context.Abort();
                return;
            } 
            var connection = new UserConnection 
            { 
                ConnectionId = Context.ConnectionId, 
                UserId = user!.Id, 
                Ip = ip!, 
                Hub = "/api/posthub",
                ConnectedAt = DateTime.UtcNow 
            };
            _db.UserConnections.Add(connection); 
            await _db.SaveChangesAsync(); 
            Log.Information("User connected: UserName: {UserName}, IP: {IPAddress}, ConnectionId: {ConnectionId}", userName, ip, Context.ConnectionId); 
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
            await Clients.User(userId).SendAsync("PostCreated", true, post);
            await Clients.AllExcept(Context.UserIdentifier).SendAsync("PostCreated", false, post);
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

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var connectionId = Context.ConnectionId;
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