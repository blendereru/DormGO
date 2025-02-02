namespace DormGO.DTOs;

public class SearchPostDto
{
    public string SearchText { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? MaxPeople { get; set; }
    public List<MemberDto> Members { get; set; } = new List<MemberDto>();
    public bool? OnlyAvailable { get; set; }
}   