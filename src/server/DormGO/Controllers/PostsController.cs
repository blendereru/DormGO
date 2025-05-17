using DormGO.Constants;
using DormGO.Data;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Filters;
using DormGO.Hubs;
using DormGO.Models;
using DormGO.Services;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DormGO.Controllers;
[Authorize]
[ApiController]
[ServiceFilter<ValidateUserEmailFilter>]
[Route("api/post")]
public class PostsController : ControllerBase
{
    private readonly ApplicationContext _db;
    private readonly IHubContext<PostHub> _hub;
    private readonly INotificationService _notificationService;
    private readonly ILogger<PostsController> _logger;
    private readonly IInputSanitizer _inputSanitizer;
    private readonly IMapper _mapper;

    public PostsController(ApplicationContext db,
        INotificationService notificationService, IHubContext<PostHub> hub, ILogger<PostsController> logger,
        IInputSanitizer inputSanitizer, IMapper mapper)
    {
        _db = db;
        _hub = hub;
        _notificationService = notificationService;
        _logger = logger;
        _inputSanitizer = inputSanitizer;
        _mapper = mapper;
    }
    [HttpPost("create")]
    public async Task<IActionResult> CreatePost([FromBody] PostRequestDto postDto)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        var connectionIds = await _db.UserConnections
            .Where(c => c.UserId == user.Id && c.Hub == "/api/posthub")
            .Select(uc => uc.ConnectionId)
            .ToListAsync();
        var post = _mapper.Map<Post>(postDto);
        post.Creator = user;
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Post successfully created by {UserId}. PostId: {PostId}", user.Id, post.Id);
        var postDtoMapped = post.Adapt<PostResponseDto>();
        await _hub.Clients.User(user.Id).SendAsync("PostCreated", true, postDtoMapped);
        await _hub.Clients.AllExcept(connectionIds).SendAsync("PostCreated", false, postDtoMapped);
        _logger.LogInformation("Message on post creation sent to users on hub. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
        return Ok(new { Message = "The post was saved to the database" });
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchPosts([FromQuery] PostSearchRequestDto postSearchRequest)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        try
        {
            var query = _db.Posts.AsQueryable();
            if (!string.IsNullOrEmpty(postSearchRequest.SearchText))
            {
                var sanitizedSearchText = _inputSanitizer.Sanitize(postSearchRequest.SearchText);
                _logger.LogDebug("Applying text filter: {SearchText}", sanitizedSearchText);
                var searchTerm = postSearchRequest.SearchText.ToLower();
                query = query.Where(p => p.Description.ToLower().Contains(searchTerm));
            }
            if (postSearchRequest.StartDate.HasValue)
            {
                _logger.LogDebug("Applying start date filter: {StartDate}", postSearchRequest.StartDate.Value);
                query = query.Where(p => p.CreatedAt >= postSearchRequest.StartDate.Value);
            }
            if (postSearchRequest.EndDate.HasValue)
            {
                _logger.LogDebug("Applying end date filter: {EndDate}", postSearchRequest.EndDate.Value);
                query = query.Where(p => p.CreatedAt <= postSearchRequest.EndDate.Value);
            }
            if (postSearchRequest.Members.Count > 0)
            {
                _logger.LogDebug("Processing member filter for {Count} emails", postSearchRequest.Members.Count);
                var memberEmails = postSearchRequest.Members.Select(m => m.Email).ToList();
                var users = await _db.Users
                    .Where(u => memberEmails.Contains(u.Email!))
                    .ToListAsync();
                if (users.Count <= 0)
                {
                    _logger.LogInformation("No users found for provided member emails");
                    return Ok(new List<PostResponseDto>());
                }
                _logger.LogDebug("Found {UserCount} matching users in database", users.Count);
                var userIds = users.Select(u => u.Id).ToList();
                foreach (var userId in userIds)
                {
                    query = query.Where(p => p.Members.Any(m => m.Id == userId));
                }
            }
            if (postSearchRequest.MaxPeople.HasValue)
            {
                _logger.LogDebug("Applying max people filter: {MaxPeople}", postSearchRequest.MaxPeople.Value);
                query = query.Where(p => p.MaxPeople <= postSearchRequest.MaxPeople.Value);
            }
            if (postSearchRequest.OnlyAvailable.HasValue && postSearchRequest.OnlyAvailable.Value)
            {
                _logger.LogDebug("Applying availability filter (only non-full posts)");
                query = query.Where(p => p.Members.Count < p.MaxPeople);
            }
            var posts = await query
                .Include(p => p.Creator)
                .Include(p => p.Members)
                .ProjectToType<PostResponseDto>()
                .ToListAsync();
            _logger.LogDebug("Search completed for {UserId}. Found {PostCount} results", 
                user.Id, posts.Count);
            return Ok(posts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchPosts: Error occurred during search for {UserId}", user.Id);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }
    [HttpGet("read")]
    public async Task<IActionResult> ReadPosts(bool joined)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        if (joined)
        {
            var postsWhereMember = await _db.Posts
                .Where(p => p.Members.Any(m => m.Id == user.Id))
                .Include(p => p.Members)
                .ProjectToType<PostResponseDto>()
                .ToListAsync();
            _logger.LogInformation("Joined posts retrieved for {UserId}. PostsCount: {PostsCount}", user.Id, postsWhereMember.Count);
            return Ok(new
            {
                postsWhereMember
            });
        }
        var yourPosts = await _db.Posts
            .Where(p => p.Creator == user)
            .Include(p => p.Members)
            .ProjectToType<PostResponseDto>()
            .ToListAsync();
        var restPosts = await _db.Posts
            .Where(p => p.Creator != user && !p.Members.Contains(user))
            .Include(p => p.Members)
            .ProjectToType<PostResponseDto>()
            .ToListAsync();
        _logger.LogInformation("Posts retrieved for {UserId}. User created posts count: {UsersPostsCount}, Other posts count: {OtherPostsCount}", user.Id, yourPosts.Count, restPosts.Count);
        return Ok(new
        {
            yourPosts,
            restPosts
        });
    }

    [HttpGet("read/{id}")]
    public async Task<IActionResult> ReadPost(string id)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        var post = await _db.Posts
            .Include(p => p.Members)
            .Include(p => p.Creator)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post == null)
        {
            _logger.LogWarning("Post reading requested for non-existent post. UserId: {UserId}", user.Id);
            return BadRequest(new { Message = "Post not found" });
        }
        var postDto = _mapper.Map<PostResponseDto>(post);
        _logger.LogInformation("Post retrieved successfully. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
        return Ok(postDto);
    }

    [HttpPut("join/{id}")]
    public async Task<IActionResult> JoinPost(string id)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        var post = await _db.Posts
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post == null)
        {
            _logger.LogWarning("Post join requested for non-existent post. UserId: {UserId}", user.Id);
            return NotFound(new { Message = "Post not found" });
        }
        if (post.CreatorId == user.Id)
        {
            _logger.LogWarning("Post join requested for user who is the owner of post. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
            return BadRequest(new { Message = "You can't join the post as you are the creator." });
        }
        if (post.Members.Any(m => m.Id == user.Id))
        {
            _logger.LogWarning("Post join requested for user who is already a member of the post. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
            return BadRequest(new { Message = "The user is already a member of the post" });
        }
        if (post.Members.Count >= post.MaxPeople)
        {
            _logger.LogWarning("Post join requested for post that reached its maximum capacity. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
            return BadRequest(new { Message = "The post has reached its maximum member capacity" });
        }
        post.Members.Add(user);
        await _db.SaveChangesAsync();
        _logger.LogInformation("User joined post successfully. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
        var updatedPostDto = _mapper.Map<PostResponseDto>(post);
        await _hub.Clients.All.SendAsync("PostJoined", updatedPostDto);
        _logger.LogInformation("Message on post join sent to users on hub. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
        return Ok(new { Message = "The user was successfully added to the members of the post" });
    }

    [HttpPut("{id}/transfer-ownership")]
    public async Task<IActionResult> TransferPostOwnership(string id, [FromBody] UserRequestDto userDto)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        var post = await _db.Posts.Include(p => p.Members).FirstOrDefaultAsync(p => p.Id == id);
        if (post == null)
        {
            _logger.LogWarning("Ownership transfer requested for non-existent post. UserId: {UserId}", user.Id);
            return NotFound(new { Message = "The post does not exist." });
        }
        if (post.CreatorId != user.Id)
        {
            _logger.LogWarning("Ownership transfer requested for user who is not the owner of post. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
            return Unauthorized(new { Message = "Only the creator can transfer ownership." });
        }
        var newOwner = post.Members.FirstOrDefault(m => m.Email == userDto.Email);
        if (newOwner == null)
        {
            _logger.LogWarning("Ownership transfer requested for non-existent new owner. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
            return BadRequest(new { Message = "The new owner must be a member of the post." });
        }
        post.CreatorId = newOwner.Id;
        post.Members.Remove(newOwner);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Ownership transferred successfully. NewOwnerId: {NewOwnerId}, PostId: {PostId}", newOwner.Id, post.Id);
        var notification = new NotificationResponseDto()
        {
            Message = $"You are now the owner of the post: {post.Title}",
            Post = _mapper.Map<PostResponseDto>(post)
        };
        await _notificationService.NotifyUserAsync(newOwner.Id, notification);
        return Ok(new { Message = "Ownership transferred successfully." });
    }
    [HttpPut("update/{id}")]
    public async Task<IActionResult> UpdatePost(string id, [FromBody] PostRequestDto postDto)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        var post = await _db.Posts
            .Include(p => p.Members)
            .Include(p => p.Creator)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post == null)
        {
            _logger.LogWarning("Post update requested for non-existent post. UserId: {UserId}", user.Id);
            return NotFound(new { Message = "The post with the specified ID was not found." });
        }
        if (post.CreatorId != user.Id)
        {
            _logger.LogWarning("Post update requested for user who is not the owner of post. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
            return BadRequest(new { Message = "You are not authorized to update this post." });
        }
        var creator = post.Creator;
        postDto.Adapt(post);
        post.Creator = creator;
        if (postDto.MembersToRemove.Any())
        {
            var memberEmails = postDto.MembersToRemove.Select(m => m.Email).ToList();
            var users = await _db.Users
                .Where(u => memberEmails.Contains(u.Email!))
                .ToListAsync();
            if (users.Count > 0)
            {
                var notification = new NotificationResponseDto()
                {
                    Message = $"You have been removed from the post: {post.Title}",
                    Post = post.Adapt<PostResponseDto>()
                };
                foreach (var removedUser in users)
                {
                    await _notificationService.NotifyUserAsync(removedUser.Id, notification);
                }
                _logger.LogInformation("Removing {Count} members from post {PostId}.", users.Count, post.Id);
                post.Members = post.Members.Except(users).ToList();
            }
        }
        post.UpdatedAt = DateTime.UtcNow;
        _db.Posts.Update(post);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Post updated successfully. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
        var updatedPostDto = post.Adapt<PostResponseDto>();
        foreach (var member in post.Members)
        {
            var notification = new NotificationResponseDto()
            {
                Message = "Post's settings were updated. Check the updates",
                Post = post.Adapt<PostResponseDto>()
            };
            await _notificationService.NotifyUserAsync(member.Id, notification);
        }
        await _hub.Clients.All.SendAsync("PostUpdated", updatedPostDto);
        _logger.LogInformation("Message on post update sent to users on hub. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
        return Ok(new { Message = "The post was successfully updated.", Post = updatedPostDto });
    }
    [HttpDelete("unjoin/{id}")]
    public async Task<IActionResult> UnjoinPost(string id)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        var post = await _db.Posts
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post == null)
        {
            _logger.LogWarning("Post unjoin requested for non-existent post. UserId: {UserId}", user.Id);
            return NotFound(new { Message = "The post with the specified ID was not found." });
        }
        if (post.CreatorId == user.Id)
        {
            if (post.Members.Count <= 0)
            {
                _db.Posts.Remove(post);
                await _db.SaveChangesAsync();
                _logger.LogInformation("Post removed because the creator left and there were no other members. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
                return Ok(new { Message = "Post deleted because there were no other members." });
                
            }
            var newCreator = post.Members.FirstOrDefault(m => m.Id != user.Id);
            if (newCreator == null)
            {
                _logger.LogWarning("No members found during post unjoin. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
                return BadRequest(new { Messsage = "Couldn't find other members of the post" });
            }
            post.CreatorId = newCreator.Id;
            _logger.LogInformation("Transferring ownership of post from {OldCreatorId} to {NewCreatorId}. PostId: {PostId}", user.Id, newCreator.Id, post.Id);
        }
        else
        {
            if (!post.Members.Any(m => m.Id == user.Id))
            {
                _logger.LogWarning("Post unjoin requested for user who is not a member of post. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
                return BadRequest(new { Message = "User is not a member of the post." });
            }
        }
        post.Members.Remove(user);
        await _db.SaveChangesAsync(); 
        _logger.LogInformation("User unjoined post successfully. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
        var updatedPostDto = _mapper.Map<PostResponseDto>(post);
        await _hub.Clients.All.SendAsync("PostUnjoined", updatedPostDto);
        _logger.LogInformation("Message on post unjoin sent to users on hub. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
        return Ok(new { Message = "The user was successfully removed from the post's members." });
    }

    [HttpDelete("delete/{id}")]
    public async Task<IActionResult> RemovePost(string id)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        var post = await _db.Posts
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post == null)
        {
            _logger.LogWarning("Post remove requested for non-existent post. UserId: {UserId}", user.Id);
            return NotFound(new { Message = "The post with the specified ID was not found." });
        }
        if (post.CreatorId != user.Id)
        {
            _logger.LogWarning("Post remove requested for user who is not the owner of post. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
            return Unauthorized(new { Message = "You are not authorized to delete this post." });
        }
        _db.Posts.Remove(post);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Post removed successfully. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
        await _hub.Clients.All.SendAsync("PostDeleted", post.Id);
        _logger.LogInformation("Message on post remove sent to users on hub. PostId: {PostId}", post.Id);
        return Ok(new { Message = "The post was successfully removed." });
    }
}