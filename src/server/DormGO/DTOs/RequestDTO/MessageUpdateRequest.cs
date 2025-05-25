using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class MessageUpdateRequest
{
    [Required]
    public string Content { get; set; }
}