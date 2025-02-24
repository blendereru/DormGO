using DormGO.DTOs;

namespace DormGO.Contracts;

public class TransferOwnership
{
    public string PostId { get; set; }
    public string UserEmail { get; set; }
    public MemberDto NewOwner { get; set; }
}