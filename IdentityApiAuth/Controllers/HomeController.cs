using System.Security.Claims;
using IdentityApiAuth.DTOs;
using IdentityApiAuth.Models;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace IdentityApiAuth.Controllers;
[Authorize]
public class HomeController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationContext _db;
    private readonly IMapper _mapper;
    public HomeController(UserManager<ApplicationUser> userManager, ApplicationContext db,
        IMapper mapper)
    {
        _userManager = userManager;
        _db = db;
        _mapper = mapper;
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
    [HttpPost("/api/post/create")]
    public async Task<IActionResult> CreatePost([FromBody] PostDto postDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        var user = await _userManager.FindByEmailAsync(postDto.Creator.Email);
        if (user == null)
        {
            return BadRequest($"User with email {postDto.Creator.Email} not found.");
        }
        var post = _mapper.Map<Post>(postDto);
        post.Members.Add(user);
        user.Posts.Add(post);
        _db.Users.Update(user);
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        return Ok(new { Message = "The post was saved to db" });
    }

}