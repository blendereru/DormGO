using System.ComponentModel.DataAnnotations;
using DormGO.Models;

namespace DormGO.DTOs;

public class PostDto
{
    public string? PostId { get; set; }
    public string Description { get; set; }
    public decimal CurrentPrice { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int MaxPeople { get; set; }
    public MemberDto? Creator { get; set; }
    public List<MemberDto> Members { get; set; } = new List<MemberDto>();
}