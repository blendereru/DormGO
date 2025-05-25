namespace DormGO.DTOs.RequestDTO;

public class PostSearchRequest
{
    public string? SearchText { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? MaxPeople { get; set; }
    public List<UserRegisterRequest> Members { get; set; } = new List<UserRegisterRequest>();
    public bool? OnlyAvailable { get; set; }
}