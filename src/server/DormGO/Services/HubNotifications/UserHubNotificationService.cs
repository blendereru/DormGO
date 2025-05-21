using DormGO.DTOs.ResponseDTO;
using DormGO.Hubs;
using DormGO.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace DormGO.Services.HubNotifications;

public class UserHubNotificationService : IUserHubNotificationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<UserHub> _hub;
    private readonly ILogger<UserHubNotificationService> _logger;

    public UserHubNotificationService(UserManager<ApplicationUser> userManager, IHubContext<UserHub> hub,
        ILogger<UserHubNotificationService> logger)
    {
        _userManager = userManager;
        _hub = hub;
        _logger = logger;
    }

    public async Task NotifyEmailChangedAsync(ApplicationUser user, UserResponseDto userResponseDto)
    {
        var notificationDto = new
        {
            userResponseDto.Email
        };
        await _hub.Clients.User(user.Id).SendAsync("EmailChanged", notificationDto);
        _logger.LogInformation("Email changed notification sent. UserId: {UserId}", user.Id);
    }

    public async Task NotifyPasswordResetLinkValidated(ApplicationUser user, UserResponseDto userResponseDto)
    {
        var notificationDto = new
        {
            userResponseDto.Email
        };
        await _hub.Clients.User(user.Id).SendAsync("PasswordResetLinkValidated", notificationDto);

        _logger.LogInformation("Password reset link validated notification sent. UserId: {UserId}", user.Id);
    }

    public async Task NotifyEmailConfirmedAsync(ApplicationUser user, UserResponseDto userResponseDto)
    {
        var notificationDto = new
        {
            userResponseDto.Email
        };
        await _hub.Clients.User(user.Id).SendAsync("EmailConfirmed", notificationDto);
        _logger.LogInformation("Email confirmed notification sent. UserId: {UserId}", user.Id);
    }
}