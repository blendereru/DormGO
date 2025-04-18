using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs;

public class EmailDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
    [Required]
    public string VisitorId { get; set; }
}