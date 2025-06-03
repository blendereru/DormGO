using DormGO.Models;

namespace DormGO.Services.HubNotifications;

public interface INotificationHubNotificationService<in TNotification> where TNotification : Notification
{
    Task NotifyNotificationSentAsync(ApplicationUser user, TNotification notification);
}