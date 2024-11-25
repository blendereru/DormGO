using Microsoft.AspNetCore.Identity;

namespace IdentityApiAuth.Models;

public class ApplicationUser : IdentityUser
{
    public DateTime RegistrationDate { get; set; }
    public IList<RefreshSession> RefreshSessions { get; set; } = new List<RefreshSession>();
    public IList<Post> Posts { get; set; } = new List<Post>();
}