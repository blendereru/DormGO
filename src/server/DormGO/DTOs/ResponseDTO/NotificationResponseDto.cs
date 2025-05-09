using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.ResponseDTO;

public class NotificationResponseDto
{
    public string NotificationId { get; set; }
    public UserResponseDto User { get; set; }
    public string Message { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public PostResponseDto Post { get; set; }
}