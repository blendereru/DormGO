using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class PostUpdateRequest
{
    [Description("Title of the post")]
    public string? Title { get; set; }
    [Description("Description of the post")]

    public string? Description { get; set; }
    [Description("Post's current price")]

    public decimal? CurrentPrice { get; set; }
    [Description("Post's destination latitude")]

    public double? Latitude { get; set; }
    [Description("Post's destination longitude")]

    public double? Longitude { get; set; }
    [Description("The maximum number of people that can join a post")]

    public int? MaxPeople { get; set; }
    [Description("Members to remove from the post")]
    public IList<UserToRemoveRequest> MembersToRemove { get; set; } = new List<UserToRemoveRequest>();
}

public class UserToRemoveRequest
{
    [Description("The unique identifier (ID) of the user to remove")]
    [Required]
    public string Id { get; set; }
}
