using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using DormGO.Constants;
using DormGO.Data;
using DormGO.DTOs.Enums;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Filters;
using DormGO.Models;
using DormGO.Services;
using DormGO.Services.HubNotifications;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DormGO.Controllers;
[Authorize]
[ApiController]
[ServiceFilter<ValidateUserEmailFilter>]
[Route("api/posts")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
public class PostController : ControllerBase
{
    private readonly ApplicationContext _db;
    private readonly IPostHubNotificationService _postHubNotificationService;
    private readonly INotificationService<PostNotification, PostNotificationResponse> _notificationService;
    private readonly ILogger<PostController> _logger;
    private readonly IInputSanitizer _inputSanitizer;

    public PostController(ApplicationContext db,
        IPostHubNotificationService postHubNotificationService,
        INotificationService<PostNotification, PostNotificationResponse> notificationService,
        ILogger<PostController> logger,
        IInputSanitizer inputSanitizer)
    {
        _db = db;
        _postHubNotificationService = postHubNotificationService;
        _notificationService = notificationService;
        _logger = logger;
        _inputSanitizer = inputSanitizer;
    }
    [EndpointSummary("Post creation")]
    [EndpointDescription("Intended to create a post in the system.")]
    [ProducesResponseType<PostResponse>(StatusCodes.Status201Created, "application/json")]
    [HttpPost]
    public async Task<IActionResult> CreatePost(PostCreateRequest postCreateRequest)
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

        var post = postCreateRequest.Adapt<Post>();
        post.Creator = user;
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Post successfully created by {UserId}. PostId: {PostId}", user.Id, post.Id);
        var postResponse = post.Adapt<PostResponse>();
        await _postHubNotificationService.NotifyPostCreatedAsync(user, post);
        return CreatedAtAction("ReadPost", new { id = post.Id }, postResponse);
    }
    [EndpointSummary("Search for specific post(s)")]
    [EndpointDescription("Intended to find post(s) based on filter applied")]
    [ProducesResponseType<List<PostResponse>>(StatusCodes.Status200OK, "application/json")]
    [HttpGet("search")]
    public async Task<IActionResult> SearchPosts([FromQuery] PostSearchRequest postSearchRequest)
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
            _logger.LogDebug("Processing member filter for {Count} members", postSearchRequest.Members.Count);
            var memberEmails = postSearchRequest.Members.Select(m => m.Email).ToList();
            var users = await _db.Users
                .Where(u => memberEmails.Contains(u.Email!))
                .ToListAsync();
            if (users.Count <= 0)
            {
                _logger.LogInformation("No users found for provided member emails");
                return Ok(new List<PostResponse>());
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
            .ProjectToType<PostResponse>()
            .ToListAsync();
        _logger.LogDebug("Search completed for {UserId}. Found {PostCount} results", 
            user.Id, posts.Count);
        return Ok(posts);
    }
    [EndpointSummary("Retrieve current user's posts")]
    [EndpointDescription("Retrieve current user's posts based on the membership filter")]
    [ProducesResponseType<List<PostResponse>>(StatusCodes.Status200OK, "application/json")]
    [HttpGet]
    public async Task<IActionResult> ReadPosts(MembershipType? membershipType = null)
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
        var result = membershipType switch
        {
            MembershipType.Joined => new
            {
                Posts = await _db.Posts
                    .Where(p => p.Members.Any(m => m.Id == user.Id))
                    .Include(p => p.Members)
                    .ProjectToType<PostResponse>()
                    .ToListAsync(),
                LogMsg = "Joined posts retrieved for {UserId}. PostsCount: {PostsCount}"
            },
            MembershipType.Own => new
            {
                Posts = await _db.Posts
                    .Where(p => p.CreatorId == user.Id)
                    .Include(p => p.Members)
                    .ProjectToType<PostResponse>()
                    .ToListAsync(),
                LogMsg = "User's own posts retrieved. UserId: {UserId}, PostsCount: {PostsCount}"
            },
            MembershipType.NotJoined => new
            {
                Posts = await _db.Posts
                    .Where(p => p.CreatorId != user.Id && !p.Members.Any(m => m.Id == user.Id))
                    .Include(p => p.Members)
                    .ProjectToType<PostResponse>()
                    .ToListAsync(),
                LogMsg = "Not joined posts retrieved for {UserId}. PostsCount: {PostsCount}"
            },
            _ => null
        };

        if (result != null)
        {
            _logger.LogInformation(result.LogMsg, user.Id, result.Posts.Count);
            return Ok(result.Posts);
        }

        var yourPostsTask = _db.Posts
            .Where(p => p.CreatorId == user.Id)
            .Include(p => p.Members)
            .ProjectToType<PostResponse>()
            .ToListAsync();

        var joinedPostsTask = _db.Posts
            .Where(p => p.Members.Any(m => m.Id == user.Id))
            .Include(p => p.Members)
            .ProjectToType<PostResponse>()
            .ToListAsync();

        var notJoinedPostsTask = _db.Posts
            .Where(p => p.CreatorId != user.Id && !p.Members.Any(m => m.Id == user.Id))
            .Include(p => p.Members)
            .ProjectToType<PostResponse>()
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
    [EndpointSummary("Retrieve a post")]
    [EndpointDescription("Retrieve post by post id")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<PostResponse>(StatusCodes.Status200OK, "application/json")]
    [HttpGet("{id}")]
    public async Task<IActionResult> ReadPost([Description("Id of the post to retrieve")] string id)
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

        var response = post.Adapt<PostResponse>();
        _logger.LogInformation("Post retrieved successfully. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
        return Ok(response);
    }
    [EndpointSummary("Join specific post")]
    [EndpointDescription("Intended to join current user to the post")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpPost("{id}/membership")]
    public async Task<IActionResult> JoinPost([Description("Id of the post to join")] string id)
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
    [EndpointSummary("Transfer a post ownership")]
    [EndpointDescription("Transfer current user's post ownership to another user")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpPut("{id}/ownership")]
    public async Task<IActionResult> TransferPostOwnership(string id, OwnershipTransferRequest ownershipTransferRequest)
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
        var sanitizedPostId = _inputSanitizer.Sanitize(id);
        var post = await _db.Posts.Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == sanitizedPostId && p.CreatorId == user.Id);
        ApplicationUser? newOwner;
        if (ownershipTransferRequest.Email != null)
        {
            newOwner = post?.Members.FirstOrDefault(m => m.Email == ownershipTransferRequest.Email);
        }
        else
        {
            if (ownershipTransferRequest.UserName == null)
            {
                _logger.LogWarning("User email and name not provided during ownership transfer. InitiatorId: {InitiatorId}, PostId: {PostId}", user.Id, sanitizedPostId);
                ModelState.AddModelError(nameof(ownershipTransferRequest.UserName), "The user name field is required");
                ModelState.AddModelError(nameof(ownershipTransferRequest.Email), "The email field is required");
                return ValidationProblem(ModelState);
            }
            newOwner = post?.Members.FirstOrDefault(m => m.UserName == ownershipTransferRequest.UserName);
        }
        if (post == null || newOwner == null)
        {
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
        await _notificationService.SendNotificationAsync(newOwner, notification, "OwnershipTransferred"); 
        return NoContent();
    }
    [EndpointSummary("Updates a post")]
    [EndpointDescription("Update post settings")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdatePost([Description("The id of the post to update")] string id, PostUpdateRequest postUpdateRequest)
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
        postUpdateRequest.Adapt(post);
        post.Creator = creator;
        if (postUpdateRequest.MembersToRemove.Any())
        {
            var memberIds = postUpdateRequest.MembersToRemove.Select(m => m.Id).ToList();
            var users = await _db.Users
                .Where(u => memberIds.Contains(u.Id))
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
                    await _notificationService.SendNotificationAsync(removedUser, notification, "PostLeft");
                }
                _logger.LogInformation("Removing {Count} members from post {PostId}.", users.Count, post.Id);
                post.Members = post.Members.Except(users).ToList();
            }
        }
        post.UpdatedAt = DateTime.UtcNow;
        _db.Posts.Update(post);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Post updated successfully. UserId: {UserId}, PostId: {PostId}", user.Id, post.Id);
        await _postHubNotificationService.NotifyPostUpdatedAsync(user, post);
        return NoContent();
    }
    [EndpointSummary("Leave the post")]
    [EndpointDescription("Intended to remove the current user from the post's members list")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpDelete("{id}/membership")]
    public async Task<IActionResult> LeavePost([Description("The post's id to leave")] string id)
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
    [EndpointSummary("Delete the post")]
    [EndpointDescription("Intended to delete the post and all of its members from the post")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePost([Description("Id of the post to delete")] string id)
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