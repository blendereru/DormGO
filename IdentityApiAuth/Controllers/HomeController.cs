using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace IdentityApiAuth.Controllers;
[Authorize]
public class HomeController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    public HomeController(UserManager<IdentityUser> userManager)
    {
        _userManager = userManager;
    }
    [HttpGet("/api/protected")]
    public IActionResult Index(ClaimsPrincipal user)
    {
        var email = user.Identity?.Name;
        return Ok($"Hello, {email}! This is a protected endpoint.");
    }
}