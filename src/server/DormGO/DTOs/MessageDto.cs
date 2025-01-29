namespace DormGO.DTOs;

public class MessageDto
{
    public string? MessageId { get; set; }
    public string Content { get; set; }
    public MemberDto? Sender { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}