using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class PasswordResetRequest
{
    [Required]
    public string Email { get; set; }

    [Required]
    public string Token { get; set; }

    [Required]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; }
}
