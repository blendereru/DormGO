using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class EmailConfirmationResendRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
    [Required]
    public string VisitorId { get; set; }
}