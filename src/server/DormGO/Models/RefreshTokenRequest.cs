using System.ComponentModel.DataAnnotations;

namespace DormGO.Models;

public class RefreshTokenRequest
{
    [Required(ErrorMessage = "RefreshToken is required.")]
    public string RefreshToken { get; set; }
}