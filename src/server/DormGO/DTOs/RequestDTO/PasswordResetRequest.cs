using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class PasswordResetRequest
{
    [Required]
    public string Email { get; set; }
    [Description("Token from link used to validate the signature of the request")]
    [Required]
    public string Token { get; set; }

    [Required]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; }
}
