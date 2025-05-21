using DormGO.DTOs.ResponseDTO;
using DormGO.Models;

namespace DormGO.Services.HubNotifications;

public interface IPostHubNotificationService
{
    Task NotifyPostCreatedAsync(ApplicationUser user, PostResponseDto postResponseDto);
    Task NotifyPostUpdatedAsync(ApplicationUser user, PostResponseDto postResponseDto);
    Task NotifyPostDeletedAsync(ApplicationUser user, string postId);
    Task NotifyPostJoinedAsync(ApplicationUser user, string postId);
    Task NotifyPostLeftAsync(ApplicationUser user, string postId);
}