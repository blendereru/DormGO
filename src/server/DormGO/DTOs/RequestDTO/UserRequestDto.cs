using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class UserRequestDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
    public string Password { get; set; }
    public string Name { get; set; }
    [Required]
    public string VisitorId { get; set; }
}