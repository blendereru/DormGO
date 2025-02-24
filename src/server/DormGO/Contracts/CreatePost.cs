using DormGO.DTOs;

namespace DormGO.Contracts;

public class CreatePost
{
    public PostDto Post { get; set; }
    public string CreatorEmail { get; set; }
}