using DormGO.DTOs.Enums;

namespace DormGO.DTOs.ResponseDTO;

public abstract class NotificationResponse
{
    public string Id { get; set; }
    public UserResponse User { get; set; }
    public string Title { get; set; } 
    public string Description { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public abstract NotificationType Type { get; }
}