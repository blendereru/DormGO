using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class MessageUpdateRequest
{
    [Description("Updated content of the message")]
    [Required]
    public string Content { get; set; }
    [Description("The date time when the message was updated")]
    public DateTime UpdatedAt = DateTime.UtcNow;
}