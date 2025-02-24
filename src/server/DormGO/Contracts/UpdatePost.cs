using DormGO.DTOs;

namespace DormGO.Contracts;

public class UpdatePost
{
    public string PostId { get; set; }
    public UpdatePostDto Post { get; set; }
    public string UserEmail { get; set; }
}