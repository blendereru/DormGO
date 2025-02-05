using DormGO.DTOs;

namespace DormGO.Services;

public interface INotificationService
{
    Task NotifyUserAsync(string userId, NotificationDto notificationDto);
}