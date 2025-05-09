using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class MessageRequestDto
{
    [Required]
    public string Content { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}