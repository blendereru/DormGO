using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs;

public class MemberDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; }
    [Required]
    public string Name { get; set; }
}