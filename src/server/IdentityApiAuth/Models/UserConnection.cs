namespace IdentityApiAuth.Models;

public class UserConnection
{
    public string ConnectionId { get; set; }
    public string UserId { get; set; }
    public string Ip { get; set; }
    public DateTime ConnectedAt { get; set; }
}