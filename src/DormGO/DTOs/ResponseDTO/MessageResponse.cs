namespace DormGO.DTOs.ResponseDTO;

public class MessageResponse
{
    public string Id { get; set; }
    public string Content { get; set; }
    public UserResponse Sender { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public PostResponse Post { get; set; }
}