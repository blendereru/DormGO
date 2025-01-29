namespace DormGO.Models;

public class Message
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SenderId { get; set; }
    public ApplicationUser Sender { get; set; }
    public string Content { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public string PostId { get; set; }
    public Post Post { get; set; }
}