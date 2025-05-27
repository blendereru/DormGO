namespace DormGO.Models;

public class UserConnection
{
    public string ConnectionId { get; set; }
    public string UserId { get; set; }
    public ApplicationUser User { get; set; }
    public string Ip { get; set; }
    public string Hub { get; set; }
    public DateTime ConnectedAt { get; set; }
}
