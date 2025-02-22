namespace DormGO.Models;

public class Post
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; }
    public string Description { get; set; }
    public decimal CurrentPrice { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public int MaxPeople { get; set; }
    public string CreatorId { get; set; }
    public ApplicationUser Creator { get; set; }
    public IList<ApplicationUser> Members { get; set; } = new List<ApplicationUser>();
    public IList<Message> Messages { get; set; } = new List<Message>();
}