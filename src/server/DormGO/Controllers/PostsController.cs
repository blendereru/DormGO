using DormGO.Constants;
using DormGO.Data;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Filters;
using DormGO.Models;
using DormGO.Services;
using DormGO.Services.HubNotifications;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DormGO.Controllers;
[Authorize]
[ApiController]
[ServiceFilter<ValidateUserEmailFilter>]
[Route("api/posts")]
public class PostsController : ControllerBase
{
    private readonly ApplicationContext _db;
    private readonly IPostHubNotificationService _postHubNotificationService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<PostsController> _logger;
    private readonly IInputSanitizer _inputSanitizer;
    private readonly IMapper _mapper;

    public PostsController(ApplicationContext db,
        IPostHubNotificationService postHubNotificationService, INotificationService notificationService,
        ILogger<PostsController> logger,
        IInputSanitizer inputSanitizer, IMapper mapper)
    {
        _db = db;
        _postHubNotificationService = postHubNotificationService;
        _notificationService = notificationService;
        _logger = logger;
        _inputSanitizer = inputSanitizer;
        _mapper = mapper;
    }
    [HttpPost]
    public async Task<IActionResult> CreatePost(PostRequestDto postDto)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("Post create attempted with missing or invalid user context.");
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User information is missing.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        var post = _mapper.Map<Post>(postDto);
        post.Creator = user;
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Post successfully created by {UserId}. PostId: {PostId}", user.Id, post.Id);
        var postDtoMapped = post.Adapt<PostResponseDto>();
        await _postHubNotificationService.NotifyPostCreatedAsync(user, postDtoMapped);
        return CreatedAtAction("ReadPost", new { id = post.Id }, postDtoMapped);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchPosts(PostSearchRequestDto postSearchRequest)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("Post search attempted with missing or invalid user context.");
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User information is missing.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
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
    [HttpGet]
    public async Task<IActionResult> ReadPosts(string? membership = null)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("Post read attempted with missing or invalid user context.");
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User information is missing.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        var filter = membership?.Trim().ToLowerInvariant();
        var result = filter switch
        {
            "joined" => new
            {
                Posts = await _db.Posts
                    .Where(p => p.Members.Any(m => m.Id == user.Id))
                    .Include(p => p.Members)
                    .ProjectToType<PostResponseDto>()
                    .ToListAsync(),
                LogMsg = "Joined posts retrieved for {UserId}. PostsCount: {PostsCount}"
            },
            "own" => new
            {
                Posts = await _db.Posts
                    .Where(p => p.CreatorId == user.Id)
                    .Include(p => p.Members)
                    .ProjectToType<PostResponseDto>()
                    .ToListAsync(),
                LogMsg = "User's own posts retrieved. UserId: {UserId}, PostsCount: {PostsCount}"
            },
            "notjoined" => new
            {
                Posts = await _db.Posts
                    .Where(p => p.CreatorId != user.Id && !p.Members.Any(m => m.Id == user.Id))
                    .Include(p => p.Members)
                    .ProjectToType<PostResponseDto>()
                    .ToListAsync(),
                LogMsg = "Not joined posts retrieved for {UserId}. PostsCount: {PostsCount}"
            },
            _ => null
        };

        if (result is not null)
        {
            _logger.LogInformation(result.LogMsg, user.Id, result.Posts.Count);
            return Ok(result.Posts);
        }
        var yourPostsTask = _db.Posts
            .Where(p => p.CreatorId == user.Id)
            .Include(p => p.Members)
            .ProjectToType<PostResponseDto>()
            .ToListAsync();

        var joinedPostsTask = _db.Posts
            .Where(p => p.Members.Any(m => m.Id == user.Id))
            .Include(p => p.Members)
            .ProjectToType<PostResponseDto>()
            .ToListAsync();

        var notJoinedPostsTask = _db.Posts
            .Where(p => p.CreatorId != user.Id && !p.Members.Any(m => m.Id == user.Id))
            .Include(p => p.Members)
            .ProjectToType<PostResponseDto>()
            .ToListAsync();

        await Task.WhenAll(yourPostsTask, joinedPostsTask, notJoinedPostsTask);

        _logger.LogInformation(
            "All posts categories retrieved for {UserId}. Own: {Own}, Joined: {Joined}, NotJoined: {NotJoined}",
            user.Id, yourPostsTask.Result.Count, joinedPostsTask.Result.Count, notJoinedPostsTask.Result.Count);

        return Ok(new
        {
            yourPosts = yourPostsTask.Result,
            joinedPosts = joinedPostsTask.Result,
            notJoinedPosts = notJoinedPostsTask.Result
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> ReadPost(string id)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("Post read attempted with missing or invalid user context.");
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User information is missing.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        var post = await _db.Posts
            .Include(p => p.Members)
            .Include(p => p.Creator)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post == null)
        {
            var sanitizedPostId = _inputSanitizer.Sanitize(id);
            _logger.LogWarning("Post read failed: post not found. UserId: {UserId}, PostId: {PostId}", user.Id, sanitizedPostId);
            var problem = new ProblemDetails
            {
                Title = "Not Found",
                Detail = "The post with the specified ID was not found.",
                Status = StatusCodes.Status404NotFound,
                Instance = $"{Request.Method} {Request.Path}"
            };
            return NotFound(problem);
        }
        var postDto = _mapper.Map<PostResponseDto>(post);
        _logger.LogInformation("Post retrieved successfully. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
        return Ok(postDto);
    }

    [HttpPost("{id}/membership")]
    public async Task<IActionResult> JoinPost(string id)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("Post join attempted with missing or invalid user context");
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User information is missing.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        var sanitizedPostId = _inputSanitizer.Sanitize(id);
        var post = await _db.Posts
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post == null || post.CreatorId == user.Id || post.Members.Any(m => m.Id == user.Id))
        {
            _logger.LogWarning("Post join failed due to not found or membership/ownership violation. UserId: {UserId}, PostId: {PostId}", user.Id, sanitizedPostId);
            var problem = new ProblemDetails
            {
                Title = "Not Found",
                Detail = "Post not found.",
                Status = StatusCodes.Status404NotFound,
                Instance = $"{Request.Method} {Request.Path}"
            };
            return NotFound(problem);
        }
        if (post.Members.Count >= post.MaxPeople)
        {
            _logger.LogWarning("Post join requested for post that reached its maximum capacity. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
            var problem = new ProblemDetails
            {
                Title = "Post Full",
                Detail = "The post has reached its maximum member capacity.",
                Status = StatusCodes.Status409Conflict,
                Instance = $"{Request.Method} {Request.Path}"
            };
            return Conflict(problem);
        }
        post.Members.Add(user);
        await _db.SaveChangesAsync();
        _logger.LogInformation("User joined post successfully. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
        await _postHubNotificationService.NotifyPostJoinedAsync(user, post.Id);
        return NoContent();
    }

    [HttpPut("{id}/ownership")]
    public async Task<IActionResult> TransferPostOwnership(string id, UserRequestDto userDto)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("Ownership transfer attempted with missing or invalid user context");
            var problem = new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User information is missing.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            };
            return Unauthorized(problem);
        }
        var post = await _db.Posts.Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id && p.CreatorId == user.Id);
        var newOwner = post?.Members.FirstOrDefault(m => m.Email == userDto.Email);
        if (post == null || newOwner == null)
        {
            var sanitizedPostId = _inputSanitizer.Sanitize(id);
            _logger.LogWarning("Ownership transfer failed: not found, not owner, or new owner not member. UserId: {UserId}, PostId: {PostId}", user.Id, sanitizedPostId);
            var problem = new ProblemDetails
            {
                Title = "Not Found",
                Detail = "The post does not exist.",
                Status = StatusCodes.Status404NotFound,
                Instance = $"{Request.Method} {Request.Path}"
            };
            return NotFound(problem);
        }
        post.CreatorId = newOwner.Id;
        post.Members.Remove(newOwner);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Ownership transferred successfully. NewOwnerId: {NewOwnerId}, PostId: {PostId}", newOwner.Id, post.Id);
        var notification = new PostNotification
        {
            Title = "Post ownership",
            Description = $"You are now the owner of the post: {post.Title}",
            Post = post
        };
        await _notificationService.SendPostNotificationAsync(user, notification, "OwnershipTransferred"); 
        return NoContent();
    }
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePost(string id, PostRequestDto postDto)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("Post update attempted with missing or invalid user context");
            var problem = new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User information is missing.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            };
            return Unauthorized(problem);
        }
        var post = await _db.Posts
            .Include(p => p.Members)
            .Include(p => p.Creator)
            .FirstOrDefaultAsync(p => p.Id == id && p.CreatorId == user.Id);
        var sanitizedPostId = _inputSanitizer.Sanitize(id);
        if (post == null)
        {
            _logger.LogWarning("Post update failed: not found or not owner. PostId: {PostId}", sanitizedPostId);
            var problem = new ProblemDetails
            {
                Title = "Not Found",
                Detail = "The post with the specified ID was not found.",
                Status = StatusCodes.Status404NotFound,
                Instance = $"{Request.Method} {Request.Path}"
            };
            return NotFound(problem);
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
                var notification = new PostNotification
                {
                    Title = "Post leave",
                    Description = $"You have been removed from the post: {post.Title}"
                };
                foreach (var removedUser in users)
                {
                    await _notificationService.SendPostNotificationAsync(removedUser, notification, "PostLeft");
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
        await _postHubNotificationService.NotifyPostUpdatedAsync(user, updatedPostDto);
        return Ok(updatedPostDto);
    }
    [HttpDelete("{id}/membership")]
    public async Task<IActionResult> LeavePost(string id)
    {
        var sanitizedPostId = _inputSanitizer.Sanitize(id);

        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("Post leave attempted with missing or invalid user context");
            var problem = new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User authentication required.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            };
            return Unauthorized(problem);
        }
        var post = await _db.Posts
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post == null)
        {
            _logger.LogWarning("Post leave failed. Post not found. PostId: {PostId}", sanitizedPostId);
            var problem = new ProblemDetails
            {
                Title = "Not Found",
                Detail = "The post with the specified ID was not found.",
                Status = StatusCodes.Status404NotFound,
                Instance = $"{Request.Method} {Request.Path}"
            };
            return NotFound(problem);
        }
        var isMember = post.Members.Any(m => m.Id == user.Id);
        var isOwner = post.CreatorId == user.Id;

        if (!isMember && !isOwner)
        {
            _logger.LogWarning("Post leave forbidden: user not a member or owner. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
            var problem = new ProblemDetails
            {
                Title = "Not Found",
                Detail = "The post with the specified ID was not found.",
                Status = StatusCodes.Status404NotFound,
                Instance = $"{Request.Method} {Request.Path}"
            };
            return NotFound(problem);
        }
        if (isOwner)
        {
            if (post.Members.Count == 0)
            {
                _db.Posts.Remove(post);
                await _db.SaveChangesAsync();
                _logger.LogInformation("Post leave succeeded: post deleted as owner left and no members remained. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
                await _postHubNotificationService.NotifyPostDeletedAsync(user, post.Id);
                return NoContent();
            }
            var newOwner = post.Members.FirstOrDefault(m => m.Id != user.Id);
            if (newOwner == null)
            {
                _logger.LogWarning("Post leave failed: could not find another member for ownership transfer. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
                var problem = new ProblemDetails
                {
                    Title = "Conflict",
                    Detail = "Could not find another member to transfer ownership.",
                    Status = StatusCodes.Status409Conflict,
                    Instance = $"{Request.Method} {Request.Path}"
                };
                return Conflict(problem);
            }
            post.CreatorId = newOwner.Id;
            _logger.LogInformation("Post leaving: ownership transferred from {OldOwnerId} to {NewOwnerId}. PostId: {PostId}", user.Id, newOwner.Id, post.Id);
        }
        post.Members.Remove(user);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Post leave succeeded: user left post. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
        await _postHubNotificationService.NotifyPostLeftAsync(user, post.Id);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePost(string id)
    {
        var sanitizedPostId = _inputSanitizer.Sanitize(id);
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("Post delete attempted with missing or invalid user context");
            var problem = new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User authentication required.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            };
            return Unauthorized(problem);
        }
        var post = await _db.Posts
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (post == null)
        {
            _logger.LogWarning("Post delete failed. Post not found. PostId: {PostId}", sanitizedPostId);
            var problem = new ProblemDetails
            {
                Title = "Not Found",
                Detail = "The post with the specified ID was not found.",
                Status = StatusCodes.Status404NotFound,
                Instance = $"{Request.Method} {Request.Path}"
            };
            return NotFound(problem);
        }
        if (post.CreatorId != user.Id)
        {
            _logger.LogWarning("Post delete forbidden: user is not the owner. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
            var problem = new ProblemDetails
            {
                Title = "Forbidden",
                Detail = "You are not authorized to delete this post.",
                Status = StatusCodes.Status403Forbidden,
                Instance = $"{Request.Method} {Request.Path}"
            };
            return StatusCode(StatusCodes.Status403Forbidden, problem);
        }
        _db.Posts.Remove(post);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Post delete succeeded. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
        await _postHubNotificationService.NotifyPostDeletedAsync(user, post.Id);
        return NoContent();
    }
}