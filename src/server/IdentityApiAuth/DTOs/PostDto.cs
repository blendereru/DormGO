using System.ComponentModel.DataAnnotations;
using IdentityApiAuth.Models;

namespace IdentityApiAuth.DTOs;

public class PostDto
{
    public string Description { get; set; }
    public decimal CurrentPrice { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int MaxPeople { get; set; }
    public MemberDto Creator { get; set; }
    public List<MemberDto> Members { get; set; } = new List<MemberDto>();
}