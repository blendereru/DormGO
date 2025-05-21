using DormGO.DTOs.Enums;

namespace DormGO.DTOs.ResponseDTO;

public class PostNotificationResponseDto : NotificationResponseDto
{
    public override NotificationType Type => NotificationType.Post;
    public PostResponseDto Post { get; set; }
}