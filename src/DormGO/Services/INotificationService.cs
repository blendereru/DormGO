using DormGO.DTOs.ResponseDTO;
using DormGO.Models;

namespace DormGO.Services;

public interface INotificationService<TNotification, TResponseDto>
    where TNotification : Notification 
    where TResponseDto : NotificationResponse
{
    Task SendNotificationAsync(ApplicationUser user, TNotification notification, string eventName);
}