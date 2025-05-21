using DormGO.Models;

namespace DormGO.Services;

public interface INotificationService
{
    Task SendPostNotificationAsync(ApplicationUser user, PostNotification postNotification, string eventName);
}