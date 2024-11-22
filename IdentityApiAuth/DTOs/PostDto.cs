using System.ComponentModel.DataAnnotations;

namespace IdentityApiAuth.DTOs;

public class PostDto
{
    [Required(ErrorMessage = "Description is required.")]
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string Description { get; set; }

    [Required(ErrorMessage = "CurrentPrice is required.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "CurrentPrice must be greater than zero.")]
    public decimal CurrentPrice { get; set; }

    [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90 degrees.")]
    public double Latitude { get; set; }

    [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180 degrees.")]
    public double Longitude { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required(ErrorMessage = "MaxPeople is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "MaxPeople must be at least 1.")]
    public int MaxPeople { get; set; }

    [Required(ErrorMessage = "Creator information is required.")]
    public UserDto Creator { get; set; }
}