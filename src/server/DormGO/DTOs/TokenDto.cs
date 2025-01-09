using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs;

public class TokenDto
{
    public string? AccessToken { get; set; }
    [Required(ErrorMessage = "RefreshToken is required.")]
    public string RefreshToken { get; set; }
}