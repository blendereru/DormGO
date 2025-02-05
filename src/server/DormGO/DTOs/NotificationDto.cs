namespace DormGO.DTOs;

public class NotificationDto
{
    public string? NotificationId { get; set; }
    public MemberDto? User { get; set; }
    public string Message { get; set; }
    public bool? IsRead { get; set; } = false;
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public PostDto? Post { get; set; }
}