namespace IdentityApiAuth.Models;

public class Event
{
    public int EventId { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; }
    public string Payload { get; set; }
}