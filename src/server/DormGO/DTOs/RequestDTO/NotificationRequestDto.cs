using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class NotificationRequestDto
{
    [Required]
    public string Message { get; set; }

    public bool IsRead { get; set; } = false;
}
