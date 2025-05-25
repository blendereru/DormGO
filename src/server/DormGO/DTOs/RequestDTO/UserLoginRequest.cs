using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class UserLoginRequest
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    [Required]
    public string Password { get; set; }
    [Required]
    public string VisitorId { get; set; }
}