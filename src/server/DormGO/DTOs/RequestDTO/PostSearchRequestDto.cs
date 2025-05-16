namespace DormGO.DTOs.RequestDTO;

public class PostSearchRequestDto
{
    public string? SearchText { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? MaxPeople { get; set; }
    public List<UserRequestDto> Members { get; set; } = new List<UserRequestDto>();
    public bool? OnlyAvailable { get; set; }
}