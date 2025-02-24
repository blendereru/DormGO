using DormGO.DTOs;

namespace DormGO.Contracts;

public class SearchPosts
{
    public SearchPostDto Filter { get; set; }
    public string UserEmail { get; set; }
}