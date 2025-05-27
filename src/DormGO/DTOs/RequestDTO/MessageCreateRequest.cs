using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class MessageCreateRequest
{
    [Description("The content(text) of the message")]
    [Required]
    public string Content { get; set; }
    [Description("The date time when the message was sent")]
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}