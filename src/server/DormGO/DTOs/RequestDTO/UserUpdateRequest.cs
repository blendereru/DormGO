namespace DormGO.DTOs.RequestDTO;

public class UserUpdateRequest
{
    public string? UserName { get; set; }
    public string? NewEmail { get; set; }
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
    public string? ConfirmNewPassword { get; set; }
}