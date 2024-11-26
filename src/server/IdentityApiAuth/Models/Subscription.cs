namespace IdentityApiAuth.Models;

public class Subscription
{
    public int SubscriptionId { get; set; }
    public string SubscriberId { get; set; }
    public string EventType { get; set; }
    public string CallbackUrl { get; set; }
    public string Secret { get; set; }
}
