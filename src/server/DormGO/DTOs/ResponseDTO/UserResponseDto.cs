using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.ResponseDTO;

public class UserResponseDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
    [Required]
    public string Name { get; set; }
}