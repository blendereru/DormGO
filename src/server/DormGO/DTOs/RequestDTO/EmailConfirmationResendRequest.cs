using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class EmailConfirmationResendRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
    [Description("User's fingerprint(device id) to ensure the credentials are not forged")]
    [Required]
    public string VisitorId { get; set; }
}