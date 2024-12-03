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
        await hub.Clients.User(user.UserName).SendAsync("PostCreated", true, postDto);
        await hub.Clients.AllExcept(user.UserName).SendAsync("PostCreated", false, postDto);
        return Ok(new { Message = "The post was saved to the database" });
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
            .Where(p => p.Creator != user && !p.Members.Contains(user))
            .Include(p => p.Members)
            .ProjectToType<PostDto>()
            .ToListAsync();
        return Ok(new
        {
            yourPosts,
            restPosts
        });
    }
    [HttpGet("/api/post/read/others")]
    public async Task<IActionResult> ReadOtherPosts()
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
        var postsWhereMember = await _db.Posts
            .Where(p => p.Members.Contains(user))
            .Include(p => p.Members)
            .ProjectToType<PostDto>()
            .ToListAsync();
        return Ok(new
        {
            postsWhereMember
        });
    }
    [HttpGet("/api/post/read/{id}")]
    public async Task<IActionResult> ReadPost(string id)
    {
        var post = await _db.Posts
            .Include(p => p.Members)
            .Include(p => p.Creator)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post == null)
        {
            return BadRequest("The post with specified id is not found");
        }
        var postDto = _mapper.Map<PostDto>(post);
        return Ok(postDto);
    }
    [HttpPost("/api/post/join/{id}")]
    public async Task<IActionResult> JoinPost(string id)
    {
        var post = await _db.Posts
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post == null)
        {
            return NotFound("The post with the specified ID is not found");
        }
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
        if (post.Members.Any(m => m.Id == user.Id))
        {
            return BadRequest("The user is already a member of the post");
        }
        if (post.Members.Count >= post.MaxPeople)
        {
            return BadRequest("The post has reached its maximum member capacity");
        }
        post.Members.Add(user);
        await _db.SaveChangesAsync();
        return Ok("The user was successfully added to the members of the post");
    }

    [HttpPost("/api/post/update/{id}")]
    public async Task<IActionResult> UpdatePost(string id, [FromBody] PostDto postDto, [FromServices] IHubContext<PostHub> hub)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        var userEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(userEmail))
        {
            return Unauthorized("User's email not found in the JWT token.");
        }
        var user = await _userManager.FindByEmailAsync(userEmail);
        if (user == null)
        {
            return Unauthorized("The user does not exist.");
        }
        var post = await _db.Posts
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post == null)
        {
            return NotFound("The post with the specified ID was not found.");
        }
        if (post.CreatorId != user.Id)
        {
            return Forbid("You are not authorized to update this post.");
        }
        postDto.Adapt(post);
        if (postDto.Members.Any())
        {
            var memberEmails = postDto.Members.Select(m => m.Email).ToList();
            var newMembers = await _db.Users
                .Where(u => memberEmails.Contains(u.Email))
                .ToListAsync();
            post.Members = newMembers;
        }
        post.UpdatedAt = DateTime.UtcNow;
        _db.Posts.Update(post);
        await _db.SaveChangesAsync();
        var updatedPostDto = post.Adapt<PostDto>();
        await hub.Clients.All.SendAsync("PostUpdated", updatedPostDto);
        return Ok(new { Message = "The post was successfully updated.", Post = updatedPostDto });
    }
    [HttpPost("/api/post/delete/{id}")]
    public async Task<IActionResult> RemovePost(string id, [FromServices] IHubContext<PostHub> hub)
    {
        var userEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(userEmail))
        {
            return Unauthorized("User's email not found in the JWT token.");
        }
        var user = await _userManager.FindByEmailAsync(userEmail);
        if (user == null)
        {
            return Unauthorized("The user does not exist.");
        }
        var post = await _db.Posts
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post == null)
        {
            return NotFound("The post with the specified ID was not found.");
        }
        if (post.CreatorId != user.Id)
        {
            return Forbid("You are not authorized to delete this post.");
        }
        _db.Posts.Remove(post);
        await _db.SaveChangesAsync();
        await hub.Clients.All.SendAsync("PostDeleted", post.Id);
        return Ok(new { Message = "The post was successfully removed." });
    }
}