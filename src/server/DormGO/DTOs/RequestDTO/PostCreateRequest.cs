using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class PostCreateRequest
{
    [Required]
    public string Title { get; set; }

    [Required]
    public string Description { get; set; }

    [Required]
    public decimal? CurrentPrice { get; set; }

    [Required]
    public double? Latitude { get; set; }

    [Required]
    public double? Longitude { get; set; }

    [Required]
    public int? MaxPeople { get; set; }
}