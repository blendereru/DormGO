using System.Security.Claims;
using IdentityApiAuth.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace IdentityApiAuth.Hubs;

public class UserHub : Hub
{
    private readonly ApplicationContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    public UserHub(ApplicationContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }
    public override async Task OnConnectedAsync()
    {
        try
        {
            var connectionId = Context.ConnectionId;
            var userId = Context?.User?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                Log.Warning("Connection aborted: Missing user id. ConnectionId: {ConnectionId}", Context.ConnectionId);
                Context.Abort();
                return;
            }
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
                UserId = user!.Id, 
                Ip = ip!, 
                Hub = "/api/userhub",
                ConnectedAt = DateTime.UtcNow 
            };
            _db.UserConnections.Add(connection);
            await _db.SaveChangesAsync();
            Log.Information("User connected: UserId: {UserId}, IP: {IPAddress}, ConnectionId: {ConnectionId}", user.Id, ip, Context.ConnectionId); 
            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred during OnConnectedAsync. ConnectionId: {ConnectionId}", Context.ConnectionId); 
            Context.Abort();
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

    public async Task NotifyEmailConfirmed(string userName, TokenRequest request)
    {
        await Clients.Caller.SendAsync("EmailConfirmed", userName, request);
    }
}