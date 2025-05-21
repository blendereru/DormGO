using DormGO.DTOs.ResponseDTO;
using DormGO.Models;

namespace DormGO.Services.HubNotifications;

public interface IChatHubNotificationService
{
    Task NotifyMessageSentAsync(ApplicationUser user, MessageResponseDto messageResponseDto);
    Task NotifyMessageUpdatedAsync(ApplicationUser user, MessageResponseDto messageResponseDto);
    Task NotifyMessageDeletedAsync(ApplicationUser user, MessageResponseDto messageResponseDto);
}