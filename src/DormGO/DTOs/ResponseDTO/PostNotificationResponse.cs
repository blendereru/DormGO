using DormGO.DTOs.Enums;

namespace DormGO.DTOs.ResponseDTO;

public class PostNotificationResponse : NotificationResponse
{
    public override NotificationType Type => NotificationType.Post;
    public PostResponse Post { get; set; }
}