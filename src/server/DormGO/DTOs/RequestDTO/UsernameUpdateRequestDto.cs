using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class UsernameUpdateRequestDto
{
    [Required]
    public string UserName { get; set; }
}