namespace DormGO.DTOs;
public class UpdatePostDto
{
    public string Description { get; set; }
    public decimal CurrentPrice { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int MaxPeople { get; set; }
    public List<MemberDto> MembersToRemove { get; set; } = new List<MemberDto>();
}