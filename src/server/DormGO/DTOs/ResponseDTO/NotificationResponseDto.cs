using DormGO.DTOs.Enums;

namespace DormGO.DTOs.ResponseDTO;

public abstract class NotificationResponseDto
{
    public string NotificationId { get; set; }
    public UserResponseDto User { get; set; }
    public string Title { get; set; } 
    public string Description { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public abstract NotificationType Type { get; }
}