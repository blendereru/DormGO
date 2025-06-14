namespace DormGO.DTOs.ResponseDTO;

public class PostCreatedNotification
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatorName { get; set; }
    public int MaxPeople { get; set; }
}