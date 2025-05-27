using DormGO.DTOs.ResponseDTO;
using DormGO.Models;

namespace DormGO.Services.HubNotifications;

public interface IUserHubNotificationService
{
    Task NotifyEmailChangedAsync(ApplicationUser user);
    Task NotifyPasswordResetLinkValidated(ApplicationUser user);
    Task NotifyEmailConfirmedAsync(ApplicationUser user, RefreshTokensResponse refreshTokensResponse);
}