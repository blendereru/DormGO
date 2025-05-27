namespace DormGO.DTOs.ResponseDTO;

public class PostResponse
{
    public string Id { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public decimal CurrentPrice { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public int MaxPeople { get; set; }

    public UserResponse Creator { get; set; }

    public List<UserResponse> Members { get; set; } = new List<UserResponse>();
}
