using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs;

public class RefreshTokenDto
{
    [Required]
    public string AccessToken { get; set; }
    [Required]
    public string RefreshToken { get; set; }
    [Required]
    public string VisitorId { get; set; }
}