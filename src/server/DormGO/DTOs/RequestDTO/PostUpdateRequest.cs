using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class PostUpdateRequest
{
    public string? Title { get; set; }

    public string? Description { get; set; }

    public decimal? CurrentPrice { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public int? MaxPeople { get; set; }
    public IList<UserToRemoveRequest> MembersToRemove { get; set; } = new List<UserToRemoveRequest>();
}

public class UserToRemoveRequest
{
    [Required]
    public string Id { get; set; }
}
