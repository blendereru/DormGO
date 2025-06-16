using DormGO.Data;
using DormGO.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DormGO.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly ApplicationContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ApplicationContext db, UserManager<ApplicationUser> userManager, ILogger<NotificationHub> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        const string hubName = nameof(NotificationHub);
        var connectionId = Context.ConnectionId;
        try
        {
            var userId = Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("[{Hub}] Connection aborted: Missing or empty user ID. ConnectionId: {ConnectionId}", hubName, connectionId);
                Context.Abort();
                return;
            }

            var ip = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrWhiteSpace(ip))
            {
                _logger.LogWarning("[{Hub}] Connection aborted: Missing IP address. UserId: {UserId}, ConnectionId: {ConnectionId}", hubName, userId, connectionId);
                Context.Abort();
                return;
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("[{Hub}] Connection aborted: User not found in database. UserId: {UserId}, ConnectionId: {ConnectionId}", hubName, userId, connectionId);
                Context.Abort();
                return;
            }

            var connection = new UserConnection
            {
                ConnectionId = connectionId,
                UserId = userId,
                Ip = ip,
                Hub = "/api/notificationhub",
                ConnectedAt = DateTime.UtcNow
            };
            _db.UserConnections.Add(connection);
            await _db.SaveChangesAsync();

            _logger.LogInformation("[{Hub}] User connected. UserId: {UserId}, IP: {IPAddress}, ConnectionId: {ConnectionId}", hubName, userId, ip, connectionId);
            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Hub}] Error occurred during OnConnectedAsync. ConnectionId: {ConnectionId}", hubName, connectionId);
            Context.Abort();
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        const string hubName = nameof(NotificationHub);
        var connectionId = Context.ConnectionId;
        try
        {
            var userId = Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (!string.IsNullOrWhiteSpace(userId))
            {
                await Groups.RemoveFromGroupAsync(connectionId, userId);
            }

            var userConnection = await _db.UserConnections.FirstOrDefaultAsync(uc => uc.ConnectionId == connectionId);
            if (userConnection != null)
            {
                _db.UserConnections.Remove(userConnection);
                await _db.SaveChangesAsync();
                _logger.LogInformation("[{Hub}] User disconnected. UserId: {UserId}, ConnectionId: {ConnectionId}", hubName, userConnection.UserId, connectionId);
            }
            else
            {
                _logger.LogWarning("[{Hub}] Connection not found in the database during disconnection. ConnectionId: {ConnectionId}", hubName, connectionId);
            }

            if (exception != null)
            {
                _logger.LogError(exception, "[{Hub}] An error occurred during disconnection. ConnectionId: {ConnectionId}", hubName, connectionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Hub}] Error occurred while processing OnDisconnectedAsync. ConnectionId: {ConnectionId}", hubName, connectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}