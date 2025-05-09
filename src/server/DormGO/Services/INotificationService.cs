using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;

namespace DormGO.Services;

public interface INotificationService
{
    Task NotifyUserAsync(string userId, NotificationResponseDto notificationDto);
}