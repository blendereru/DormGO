using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class EmailUpdateRequestDto
{
    [Required]
    [EmailAddress]
    public string NewEmail { get; set; }
}