using System.ComponentModel.DataAnnotations;

namespace IdentityApiAuth.Models;

public class RefreshTokenRequest
{
    [Required(ErrorMessage = "RefreshToken is required.")]
    public string RefreshToken { get; set; }
}