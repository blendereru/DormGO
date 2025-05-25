using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class UserLogoutRequest
{
    [Description("Token to identify the current user's session")]
    [Required]
    public string RefreshToken { get; set; } 
    [Description("A unique identifier representing the user's browser or device. This is used to associate the session with a specific environment.")]
    [Required]
    public string VisitorId { get; set; }
}