using System.ComponentModel.DataAnnotations;

namespace IdentityApiAuth.DTOs;

public class UserDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
    [Required]
    public string Password { get; set; }
}
