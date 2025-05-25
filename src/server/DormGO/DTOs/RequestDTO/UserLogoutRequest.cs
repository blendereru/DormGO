using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class UserLogoutRequest
{
    [Required]
    public string RefreshToken { get; set; } 
    [Required]
    public string VisitorId { get; set; }
}