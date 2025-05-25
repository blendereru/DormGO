using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class PasswordForgotRequest
{
    [Required]
    public string Email { get; set; }
}