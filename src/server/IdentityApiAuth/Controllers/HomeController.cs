using System.Security.Claims;
using IdentityApiAuth.DTOs;
using IdentityApiAuth.Hubs;
using IdentityApiAuth.Models;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

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
    public async Task<IActionResult> CreatePost([FromBody] PostDto postDto, [FromServices] IHubContext<PostHub> hub)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        var creatorEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
        if (creatorEmail == null)
        {
            return BadRequest("The user's email was not found from jwt token");
        }
        var user = await _userManager.FindByEmailAsync(creatorEmail.Value);
        if (user == null)
        {
            return BadRequest($"User with email {creatorEmail.Value} not found.");
        }
        var post = _mapper.Map<Post>(postDto);
        post.Creator = user;
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        await hub.Clients.All.SendAsync("PostCreated", user.UserName, postDto);
        return Ok(new { Message = "The post was saved to db" });
    }
    [HttpGet("/api/post/read")]
    public async Task<IActionResult> ReadPosts()
    {
        var emailClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
        if (emailClaim == null)
        {
            return Unauthorized("The email claim is not found");
        }

        var user = await _userManager.FindByEmailAsync(emailClaim.Value);
        if (user == null)
        {
            return NotFound("The user is not found");
        }
        var yourPosts = await _db.Posts
            .Where(p => p.Creator == user)
            .Include(p => p.Members)
            .ProjectToType<PostDto>()
            .ToListAsync();
        var restPosts = await _db.Posts
            .Where(p => p.Creator != user && p.Members.Contains(user))
            .Include(p => p.Members)
            .ProjectToType<PostDto>()
            .ToListAsync();
        return Ok(new
        {
            yourPosts,
            restPosts
        });
    }
}