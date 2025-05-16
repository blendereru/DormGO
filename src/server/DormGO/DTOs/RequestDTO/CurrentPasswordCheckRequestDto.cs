using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class CurrentPasswordCheckRequestDto
{
    [Required]
    public string CurrentPassword { get; set; }
}