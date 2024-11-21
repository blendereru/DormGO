using Microsoft.AspNetCore.Identity;

namespace IdentityApiAuth.Models;

public class ApplicationUser : IdentityUser
{
    public IList<Post> Posts { get; set; } = new List<Post>();
}