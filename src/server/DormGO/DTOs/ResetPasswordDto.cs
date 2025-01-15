using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs;

public class ResetPasswordDto
{
    [Required]
    public string Email { get; set; }

    [Required]
    public string Token { get; set; }

    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; }
}
