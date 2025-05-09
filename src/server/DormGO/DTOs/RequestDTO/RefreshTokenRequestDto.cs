using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class RefreshTokenRequestDto
{
    public string AccessToken { get; set; }
    [Required]
    public string RefreshToken { get; set; }
    [Required]
    public string VisitorId { get; set; }
}