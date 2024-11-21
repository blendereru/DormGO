using System.Security.Claims;
using IdentityApiAuth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace IdentityApiAuth.Controllers;
[Authorize]
public class HomeController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    public HomeController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }
    [HttpGet("/api/protected")]
    public IActionResult Index()
    {
        var emailClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
        var nameClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
        return Json(new
        {
            Email = emailClaim?.Value,
            Name = nameClaim?.Value,
            Role = roleClaim?.Value
        });
    }
    // //ToDO: show posts to all users in the system
    // [HttpPost("/api/post/create")]
    // public async Task<IActionResult> CreatePost([FromBody] PostModel postModel)
    // {
    //     ArgumentNullException.ThrowIfNull(postModel, nameof(postModel));
    //     ArgumentNullException.ThrowIfNull(postModel.Creator, nameof(postModel.Creator));
    //     var user = await _userManager.FindByEmailAsync(postModel.Creator.Email);
    //     
    // }
}