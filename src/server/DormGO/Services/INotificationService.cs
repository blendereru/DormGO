using DormGO.DTOs.ResponseDTO;
using DormGO.Models;

namespace DormGO.Services;

public interface INotificationService<TNotification, TResponseDto>
    where TNotification : Notification 
    where TResponseDto : NotificationResponseDto
{
    Task SendNotificationAsync(ApplicationUser user, TNotification notification, string eventName);
}