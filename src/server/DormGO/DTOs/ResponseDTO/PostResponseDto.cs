namespace DormGO.DTOs.ResponseDTO;

public class PostResponseDto
{
    public string PostId { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public decimal CurrentPrice { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public int MaxPeople { get; set; }

    public UserResponseDto Creator { get; set; }

    public List<UserResponseDto> Members { get; set; } = new List<UserResponseDto>();
}
