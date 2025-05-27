using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class PasswordForgotRequest
{
    [Description("Current user's email to which the verification link is sent to")]
    [Required]
    public string Email { get; set; }
}