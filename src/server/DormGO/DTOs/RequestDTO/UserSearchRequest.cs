using System.ComponentModel.DataAnnotations;

namespace DormGO.DTOs.RequestDTO;

public class UserSearchRequest
{
    [Required]
    public string Id { get; set; }
}