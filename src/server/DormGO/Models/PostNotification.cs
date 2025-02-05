namespace DormGO.Models;

public class PostNotification : Notification
{
    public string PostId { get; set; }
    public Post Post { get; set; }
}