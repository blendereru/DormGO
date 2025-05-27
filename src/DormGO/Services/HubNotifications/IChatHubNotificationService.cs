using DormGO.DTOs.ResponseDTO;
using DormGO.Models;

namespace DormGO.Services.HubNotifications;

public interface IChatHubNotificationService
{
    Task NotifyMessageSentAsync(ApplicationUser user, Message message);
    Task NotifyMessageUpdatedAsync(ApplicationUser user, Message message);
    Task NotifyMessageDeletedAsync(ApplicationUser user, Message message);
}