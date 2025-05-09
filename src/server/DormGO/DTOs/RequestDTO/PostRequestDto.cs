using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class PostRequestDto
{
    [Required]
    public string Title { get; set; }

    [Required]
    public string Description { get; set; }

    [Required]
    public decimal CurrentPrice { get; set; }

    [Required]
    public double Latitude { get; set; }

    [Required]
    public double Longitude { get; set; }

    [Required]
    public int MaxPeople { get; set; }

    public IList<UserRequestDto> MembersToRemove { get; set; } = new List<UserRequestDto>();
}
