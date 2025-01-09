namespace DormGO.Models;

public class RefreshSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; }
    public ApplicationUser User { get; set; }
    public string RefreshToken { get; set; }
    public string Fingerprint { get; set; }
    public string UA { get; set; } // User-Agent
    public string Ip { get; set; }
    public long ExpiresIn { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}