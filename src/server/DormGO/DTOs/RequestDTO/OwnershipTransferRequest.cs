using System.ComponentModel;

namespace DormGO.DTOs.RequestDTO;

public class OwnershipTransferRequest
{
    [Description("Email of the user to tranfer the ownership to")]
    public string? Email { get; set; }
    [Description("Username of the user to transfer the ownership to")]
    public string? UserName { get; set; }
}