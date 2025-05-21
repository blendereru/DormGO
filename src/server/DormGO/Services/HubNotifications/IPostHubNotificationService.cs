using DormGO.Models;

namespace DormGO.Services.HubNotifications;

public interface IPostHubNotificationService
{
    Task NotifyPostCreatedAsync(ApplicationUser user, Post post);
    Task NotifyPostUpdatedAsync(ApplicationUser user, Post post);
    Task NotifyPostDeletedAsync(ApplicationUser user, string postId);
    Task NotifyPostJoinedAsync(ApplicationUser user, string postId);
    Task NotifyPostLeftAsync(ApplicationUser user, string postId);
}