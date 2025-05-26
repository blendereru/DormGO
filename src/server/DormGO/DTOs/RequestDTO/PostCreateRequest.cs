using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class PostCreateRequest
{
    [Description("Title of the post")]
    [Required]
    public string Title { get; set; }
    [Description("Post description")]
    [Required]
    public string Description { get; set; }
    [Description("The general price for group")]
    [Required]
    public decimal? CurrentPrice { get; set; }
    [Description("The destination latitude")]
    [Required]
    public double? Latitude { get; set; }
    [Description("The destination longitude")]
    [Required]
    public double? Longitude { get; set; }
    [Description("The maximum number of people that can join a post")]
    [Required]
    public int? MaxPeople { get; set; }
    [Description("The date of post creation")]
    [DataType(DataType.Date)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}