using DormGO.DTOs.ResponseDTO;
using DormGO.Models;

namespace DormGO.Services.Notifications;

public interface IUserNotificationService
{
    Task NotifyEmailChangedAsync(ApplicationUser user, UserResponseDto userResponseDto);
    Task NotifyPasswordResetLinkValidated(ApplicationUser user, UserResponseDto userResponseDto);
    Task NotifyEmailConfirmedAsync(ApplicationUser user, UserResponseDto userResponseDto);
}