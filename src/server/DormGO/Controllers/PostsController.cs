using System.Security.Claims;
using DormGO.Data;
using DormGO.DTOs;
using DormGO.Hubs;
using DormGO.Models;
using DormGO.Services;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DormGO.Controllers;

[Authorize]
[ApiController]
[Route("api/post")]
public class PostsController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationContext _db;
    private readonly IHubContext<PostHub> _hub;
    private readonly INotificationService _notificationService;
    private readonly IMapper _mapper;
    public PostsController(UserManager<ApplicationUser> userManager, ApplicationContext db,
        INotificationService notificationService, IHubContext<PostHub> hub, IMapper mapper)
    {
        _userManager = userManager;
        _db = db;
        _hub = hub;
        _notificationService = notificationService;
        _mapper = mapper;
    }
    [HttpPost("create")]
    public async Task<IActionResult> CreatePost([FromBody] PostDto postDto)
    {
        var creatorEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
        if (creatorEmail == null)
        {
            Log.Warning("CreatePost: Missing email claim.");
            return BadRequest(new { Message = "The user's email was not found from jwt token" });
        }
        var user = await _userManager.FindByEmailAsync(creatorEmail.Value);
        if (user == null)
        {
            Log.Warning("CreatePost: User not found with email: {Email}", creatorEmail);
            return BadRequest(new { Message = $"User with email {creatorEmail.Value} not found." });
        }
        var connectionIds = await _db.UserConnections
            .Where(c => c.UserId == user.Id && c.Hub == "/api/posthub")
            .Select(uc => uc.ConnectionId)
            .ToListAsync();
        var post = _mapper.Map<Post>(postDto);
        post.Creator = user;
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        var postDtoMapped = post.Adapt<PostDto>();
        await _hub.Clients.User(user.Id).SendAsync("PostCreated", true, postDtoMapped);
        await _hub.Clients.AllExcept(connectionIds).SendAsync("PostCreated", false, postDtoMapped);
        Log.Information("Post created successfully by {Email}, Post ID: {PostId}", creatorEmail, post.Id);
        return Ok(new { Message = "The post was saved to the database" });
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchPosts([FromQuery] SearchPostDto searchDto)
    {
        var emailClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
        if (emailClaim == null)
        {
            Log.Warning("SearchPosts: Email claim not found in JWT token");
            return Unauthorized(new { Message = "The email claim is not found" });
        }
        var user = await _userManager.FindByEmailAsync(emailClaim.Value);
        if (user == null)
        {
            Log.Warning("SearchPosts: User not found with email: {Email}", emailClaim.Value);
            return NotFound(new { Message = "The user is not found" });
        }
        Log.Information("SearchPosts: Search initiated by {Email} with filters - Text: {SearchText}, Dates: {StartDate}-{EndDate}, MaxPeople: {MaxPeople}, Members: {MemberCount}",
            emailClaim.Value,
            searchDto.SearchText,
            searchDto.StartDate,
            searchDto.EndDate,
            searchDto.MaxPeople,
            searchDto.Members.Count);
        try
        {
            var query = _db.Posts.AsQueryable();
            if (!string.IsNullOrEmpty(searchDto.SearchText))
            {
                Log.Debug("Applying text filter: {SearchText}", searchDto.SearchText);
                var searchTerm = searchDto.SearchText.ToLower();
                query = query.Where(p => p.Description.ToLower().Contains(searchTerm));
            }
            if (searchDto.StartDate.HasValue)
            {
                Log.Debug("Applying start date filter: {StartDate}", searchDto.StartDate.Value);
                query = query.Where(p => p.CreatedAt >= searchDto.StartDate.Value);
            }
            if (searchDto.EndDate.HasValue)
            {
                Log.Debug("Applying end date filter: {EndDate}", searchDto.EndDate.Value);
                query = query.Where(p => p.CreatedAt <= searchDto.EndDate.Value);
            }
            if (searchDto.Members.Count > 0)
            {
                Log.Debug("Processing member filter for {Count} emails", searchDto.Members.Count);
                var memberEmails = searchDto.Members.Select(m => m.Email).ToList();
                var users = await _db.Users
                    .Where(u => memberEmails.Contains(u.Email))
                    .ToListAsync();
                if (!users.Any())
                {
                    Log.Information("No users found for provided member emails");
                    return Ok(new List<PostDto>());
                }
                Log.Debug("Found {UserCount} matching users in database", users.Count);
                var userIds = users.Select(u => u.Id).ToList();
                foreach (var userId in userIds)
                {
                    query = query.Where(p => p.Members.Any(m => m.Id == userId));
                }
            }
            if (searchDto.MaxPeople.HasValue)
            {
                Log.Debug("Applying max people filter: {MaxPeople}", searchDto.MaxPeople.Value);
                query = query.Where(p => p.MaxPeople <= searchDto.MaxPeople.Value);
            }
            if (searchDto.OnlyAvailable.HasValue && searchDto.OnlyAvailable.Value)
            {
                Log.Debug("Applying availability filter (only non-full posts)");
                query = query.Where(p => p.Members.Count < p.MaxPeople);
            }
            var posts = await query
                .Include(p => p.Creator)
                .Include(p => p.Members)
                .ProjectToType<PostDto>()
                .ToListAsync();
            Log.Information("Search completed for {Email}. Found {PostCount} results", 
                emailClaim.Value, posts.Count);
            return Ok(posts);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SearchPosts: Error occurred during search for {Email}", emailClaim.Value);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }
    [HttpGet("read")]
    public async Task<IActionResult> ReadPosts(bool joined)
    {
        var emailClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
        if (emailClaim == null)
        {
            Log.Warning("ReadPosts: Email claim not found.");
            return Unauthorized(new {Message = "The email claim is not found" });
        }

        var user = await _userManager.FindByEmailAsync(emailClaim.Value);
        if (user == null)
        {
            Log.Warning("ReadPosts: User not found with email: {Email}", emailClaim.Value);
            return NotFound(new { Message = "The user is not found" });
        }

        if (joined)
        {
            var postsWhereMember = await _db.Posts
                .Where(p => p.Members.Any(m => m.Id == user.Id))
                .Include(p => p.Members)
                .ProjectToType<PostDto>()
                .ToListAsync();
            Log.Information("ReadPosts: Retrieved joined posts for user {Email}.", emailClaim.Value);
            return Ok(new
            {
                postsWhereMember
            });
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
        Log.Information("ReadPosts: Retrieved posts for user {Email}.", emailClaim.Value);
        return Ok(new
        {
            yourPosts,
            restPosts
        });
    }

    [HttpGet("read/{id}")]
    public async Task<IActionResult> ReadPost(string id)
    {
        Log.Information("ReadPost: reading post with id: {PostId}", id);
        var post = await _db.Posts
            .Include(p => p.Members)
            .Include(p => p.Creator)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post == null)
        {
            Log.Warning("ReadPost: Post with id {Id} is not found", id);
            return BadRequest(new { Message = "The post with specified id is not found" });
        }
        var postDto = _mapper.Map<PostDto>(post);
        Log.Information("ReadPost: successfully read post: {Id}", id);
        return Ok(postDto);
    }

    [HttpPut("join/{id}")]
    public async Task<IActionResult> JoinPost(string id)
    {
        var post = await _db.Posts
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post == null)
        {
            Log.Warning("JoinPost: Post with id {Id} is not found", id);
            return NotFound(new { Message = "The post with the specified ID is not found" });
        }
        var emailClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
        if (emailClaim == null)
        {
            Log.Warning("JoinPost: Missing email claim.");
            return Unauthorized(new {Message = "The email claim is not found" });
        }
        var user = await _userManager.FindByEmailAsync(emailClaim.Value);
        if (user == null)
        {
            Log.Warning("JoinPost: User not found with email: {Email}", emailClaim.Value);
            return NotFound(new { Message = "The user is not found" });
        }

        if (post.CreatorId == user.Id)
        {
            Log.Warning("JoinPost: Attempt to join the post created by user himself. PostId: {PostId}, Email: {Email}", post.Id, user.Email);
            return BadRequest(new { Message = "You can't join the post as you are the creator." });
        }
        if (post.Members.Any(m => m.Id == user.Id))
        {
            Log.Warning("JoinPost: User with email {Email} is already a member of the post", user.Email);
            return BadRequest(new { Message = "The user is already a member of the post" });
        }
        if (post.Members.Count >= post.MaxPeople)
        {
            Log.Warning("JoinPost: Post has reached its capacity: {PostId}", post.Id);
            return BadRequest(new { Message = "The post has reached its maximum member capacity" });
        }
        post.Members.Add(user);
        await _db.SaveChangesAsync();
        var updatedPostDto = _mapper.Map<PostDto>(post);
        await _hub.Clients.All.SendAsync("PostJoined", updatedPostDto);
        Log.Warning("JoinPost: The user with email {Email} is a member of the post", user.Email);
        return Ok(new { Message = "The user was successfully added to the members of the post" });
    }

    [HttpPut("{id}/transfer-ownership")]
    public async Task<IActionResult> TransferPostOwnership(string id, [FromBody] MemberDto memberDto)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            Log.Warning("TransferOwnership: Unauthorized access attempt.");
            return Unauthorized(new { Message = "User is not authenticated." });
        }
        var post = await _db.Posts.Include(p => p.Members).FirstOrDefaultAsync(p => p.Id == id);
        if (post == null)
        {
            Log.Warning("TransferOwnership: Post not found. PostId: {PostId}", id);
            return NotFound(new { Message = "The post does not exist." });
        }
        if (post.CreatorId != userId)
        {
            Log.Warning("TransferOwnership: User {UserId} is not the creator of post {PostId}.", userId, id);
            return Unauthorized(new { Message = "Only the creator can transfer ownership." });
        }
        var newOwner = post.Members.FirstOrDefault(m => m.Email == memberDto.Email);
        if (newOwner == null)
        {
            Log.Warning("TransferOwnership: New owner {NewOwnerEmail} is not a member of post {PostId}.", memberDto.Email, id);
            return BadRequest(new { Message = "The new owner must be a member of the post." });
        }
        post.CreatorId = newOwner.Id;
        post.Members.Remove(newOwner);
        await _db.SaveChangesAsync();
        var notification = new NotificationDto()
        {
            Message = $"You are now the owner of the post: {post.Title}",
            Post = _mapper.Map<PostDto>(post)
        };
        await _notificationService.NotifyUserAsync(newOwner.Id, notification);
        Log.Information("Ownership of Post {PostId} transferred from {OldOwner} to {NewOwner}.",
            id, userId, newOwner.Id);
        return Ok(new { Message = "Ownership transferred successfully." });
    }
    [HttpPut("update/{id}")]
    public async Task<IActionResult> UpdatePost(string id, [FromBody] UpdatePostDto postDto)
    {
        Log.Information("UpdatePost called with post ID: {PostId} by user: {UserEmail}", id, User.Identity?.Name);
        var userEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(userEmail))
        {
            Log.Warning("Unauthorized attempt to update post. User's email is missing in the JWT token.");
            return Unauthorized(new { Message = "User's email not found in the JWT token." });
        }
        var user = await _userManager.FindByEmailAsync(userEmail);
        if (user == null)
        {
            Log.Warning("Unauthorized attempt to update post. User with email {Email} does not exist.", userEmail);
            return Unauthorized(new { Message = "The user does not exist." });
        }
        var post = await _db.Posts
            .Include(p => p.Members)
            .Include(p => p.Creator)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post == null)
        {
            Log.Warning("UpdatePost failed. Post with ID {PostId} was not found.", id);
            return NotFound(new { Message = "The post with the specified ID was not found." });
        }
        if (post.CreatorId != user.Id)
        {
            Log.Warning("Unauthorized update attempt by user {UserEmail} on post {PostId}.", userEmail, id);
            return BadRequest(new { Message = "You are not authorized to update this post." });
        }
        Log.Information("Updating post {PostId} by user {UserId}.", id, user.Id);
        var creator = post.Creator;
        postDto.Adapt(post);
        post.Creator = creator;
        if (postDto.MembersToRemove.Any())
        {
            var memberEmails = postDto.MembersToRemove.Select(m => m.Email).ToList();
            var users = await _db.Users
                .Where(u => memberEmails.Contains(u.Email))
                .ToListAsync();
            if (users.Any())
            {
                var notification = new NotificationDto()
                {
                    Message = $"You have been removed from the post: {post.Title}",
                    Post = post.Adapt<PostDto>()
                };
                foreach (var removedUser in users)
                {
                    await _notificationService.NotifyUserAsync(removedUser.Id, notification);
                }
                Log.Information("Removing {Count} members from post {PostId}.", users.Count, id);
                post.Members = post.Members.Except(users).ToList();
            }
        }
        post.UpdatedAt = DateTime.UtcNow;
        _db.Posts.Update(post);
        await _db.SaveChangesAsync();
        var updatedPostDto = post.Adapt<PostDto>();
        Log.Information("Post {PostId} updated successfully by user {UserId}.", id, user.Id);
        foreach (var member in post.Members)
        {
            var notification = new NotificationDto()
            {
                Message = "Post's settings were updated. Check the updates",
                Post = post.Adapt<PostDto>()
            };
            await _notificationService.NotifyUserAsync(member.Id, notification);
        }
        await _hub.Clients.All.SendAsync("PostUpdated", updatedPostDto);
        return Ok(new { Message = "The post was successfully updated.", Post = updatedPostDto });
    }
    [HttpDelete("unjoin/{id}")]
    public async Task<IActionResult> UnjoinPost(string id)
    {
        Log.Information("UnjoinPost called for post ID: {PostId} by user: {UserEmail}", id, User.Identity?.Name);
        var emailClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(emailClaim))
        {
            Log.Warning("Unauthorized attempt to unjoin post. User's email is missing in the JWT token.");
            return Unauthorized(new { Message = "User's email not found in the JWT token." });
        }
        var user = await _userManager.FindByEmailAsync(emailClaim);
        if (user == null)
        {
            Log.Warning("UnjoinPost failed. User with email {Email} does not exist.", emailClaim);
            return NotFound(new { Message = "The user does not exist." });
        }
        var post = await _db.Posts
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post == null)
        {
            Log.Warning("UnjoinPost failed. Post with ID {PostId} was not found.", id);
            return NotFound(new { Message = "The post with the specified ID was not found." });
        }
        if (post.CreatorId == user.Id)
        {
            if (post.Members.Count <= 0)
            {
                _db.Posts.Remove(post);
                await _db.SaveChangesAsync();
                Log.Information("Post {PostId} deleted because the creator left and there were no other members.", id);
                return Ok(new { Message = "Post deleted because there were no other members." });
                
            }
            var newCreator = post.Members.FirstOrDefault(m => m.Id != user.Id);
            if (newCreator == null)
            {
                return BadRequest(new { Messsage = "Couldn't find other members of the post" });
            }
            post.CreatorId = newCreator.Id;
            Log.Information("Ownership of Post {PostId} transferred from User {OldCreatorId} to User {NewCreatorId}.",
                id, user.Id, newCreator.Id);
        }
        else
        {
            if (!post.Members.Any(m => m.Id == user.Id))
            {
                Log.Warning("UnjoinPost failed. User {UserId} is not a member of post {PostId}.", user.Id, id);
                return BadRequest(new { Message = "The user is not a member of the post." });
            }
        }
        post.Members.Remove(user);
        await _db.SaveChangesAsync(); 
        var updatedPostDto = _mapper.Map<PostDto>(post);
        await _hub.Clients.All.SendAsync("PostUnjoined", updatedPostDto);
        Log.Information("User {UserId} successfully removed from post {PostId}.", user.Id, id);
        return Ok(new { Message = "The user was successfully removed from the post's members." });
    }

    [HttpDelete("delete/{id}")]
    public async Task<IActionResult> RemovePost(string id)
    {
        Log.Information("RemovePost: Request received to delete post with ID {PostId} by user {UserEmail}.", id, User.Identity?.Name);

        var userEmail = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(userEmail))
        {
            Log.Warning("RemovePost: Unauthorized attempt to delete post with ID {PostId}. User's email is missing in the JWT token.", id);
            return Unauthorized(new { Message = "User's email not found in the JWT token." });
        }
        var user = await _userManager.FindByEmailAsync(userEmail);
        if (user == null)
        {
            Log.Warning("RemovePost: Unauthorized attempt to delete post with ID {PostId}. User with email {UserEmail} does not exist.", id, userEmail);
            return Unauthorized(new { Message = "The user does not exist." });
        }
        var post = await _db.Posts
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post == null)
        {
            Log.Warning("RemovePost: Post with ID {PostId} not found.", id);
            return NotFound(new { Message = "The post with the specified ID was not found." });
        }
        if (post.CreatorId != user.Id)
        {
            Log.Warning("RemovePost: Unauthorized attempt by user {UserEmail} to delete post {PostId}.", userEmail, id);
            return Unauthorized(new { Message = "You are not authorized to delete this post." });
        }
        Log.Information("RemovePost: Deleting post with ID {PostId} by user {UserId}.", id, user.Id);
        _db.Posts.Remove(post);
        await _db.SaveChangesAsync();
        Log.Information("RemovePost: Post with ID {PostId} successfully deleted by user {UserId}.", id, user.Id);
        await _hub.Clients.All.SendAsync("PostDeleted", post.Id);
        return Ok(new { Message = "The post was successfully removed." });
    }
}