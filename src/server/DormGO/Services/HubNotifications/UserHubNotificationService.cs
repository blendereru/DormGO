using DormGO.Data;
using DormGO.DTOs.ResponseDTO;
using DormGO.Hubs;
using DormGO.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DormGO.Services.HubNotifications;

public class UserHubNotificationService : IUserHubNotificationService
{
    private readonly ApplicationContext _db;
    private readonly IHubContext<UserHub> _hub;
    private readonly ILogger<UserHubNotificationService> _logger;

    public UserHubNotificationService(ApplicationContext db,
        IHubContext<UserHub> hub,
        ILogger<UserHubNotificationService> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    public async Task NotifyEmailChangedAsync(ApplicationUser user)
    {
        var notificationDto = new
        {
            user.Email
        };
        await _hub.Clients.User(user.Id).SendAsync("EmailChanged", notificationDto);
        _logger.LogInformation("Email changed notification sent. UserId: {UserId}", user.Id);
    }

    public async Task NotifyPasswordResetLinkValidated(ApplicationUser user)
    {
        var notificationDto = new
        {
           user.Email
        };
        await _hub.Clients.User(user.Id).SendAsync("PasswordResetLinkValidated", notificationDto);

        _logger.LogInformation("Password reset link validated notification sent. UserId: {UserId}", user.Id);
    }

    public async Task NotifyEmailConfirmedAsync(ApplicationUser user, RefreshTokensResponse refreshTokensResponse)
    {
        var notificationDto = new
        {
            user.Email,
            refreshTokensResponse.AccessToken,
            refreshTokensResponse.RefreshToken
        };
        var connectionIds = await _db.UserConnections
            .Where(c => c.UserId == user.Id && c.Hub == "/api/userhub")
            .Select(uc => uc.ConnectionId)
            .ToListAsync();
        await _hub.Clients.Clients(connectionIds).SendAsync("EmailConfirmed", notificationDto);
        _logger.LogInformation("Email confirmed notification sent. UserId: {UserId}", user.Id);
    }
}