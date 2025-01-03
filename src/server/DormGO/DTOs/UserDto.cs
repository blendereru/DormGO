using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs;

public class UserDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
    [Required]
    public string Password { get; set; }
}
