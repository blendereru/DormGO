namespace DormGO.DTOs.ResponseDTO;

public class MessageResponseDto
{
    public string MessageId { get; set; }
    public string Content { get; set; }
    public UserResponseDto Sender { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; } 
}