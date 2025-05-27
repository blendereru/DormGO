using System.ComponentModel;

namespace DormGO.DTOs.RequestDTO;

public class UserUpdateRequest
{
    [Description("User name to update")]
    public string? UserName { get; set; }
    [Description("New email to be updated to")]
    public string? NewEmail { get; set; }
    [Description("Current password check. Set if in need to update password")]
    public string? CurrentPassword { get; set; }
    [Description("New password to be updated to")]
    public string? NewPassword { get; set; }
    [Description("Confirm new password entered")]
    public string? ConfirmNewPassword { get; set; }
}