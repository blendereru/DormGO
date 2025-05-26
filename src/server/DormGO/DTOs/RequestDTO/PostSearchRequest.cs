using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class PostSearchRequest
{
    [Description("Filtering based on post title or description")]
    public string? SearchText { get; set; }
    [Description("Filtering based on post start date")]
    public DateTime? StartDate { get; set; }
    [Description("Filtering based on post end date")]
    public DateTime? EndDate { get; set; }
    [Description("Filtering based on maximum number of people")]
    public int? MaxPeople { get; set; }
    [Description("Filtering based on members of the post")]
    public List<UserToSearchRequest> Members { get; set; } = new List<UserToSearchRequest>();
    [Description("Filtering only available posts. That is posts that didn't reach maximum people capacity")]
    public bool? OnlyAvailable { get; set; }
}

public class UserToSearchRequest
{
    [Description("Filtering based on post's users. Filter by email")]
    public string? Email { get; set; }
    [Description("Filtering based on post's users. Filter by user name")]
    public string? UserName { get; set; }
}