using System.Net;
using DormGO.Contracts;
using DormGO.Data;
using DormGO.DTOs;
using DormGO.Models;
using Mapster;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DormGO.Components;

public class ReadPostConsumer : IConsumer<ReadPost>
{
    private readonly ApplicationContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ReadPostConsumer> _logger;

    public ReadPostConsumer(ApplicationContext db, UserManager<ApplicationUser> userManager, ILogger<ReadPostConsumer> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReadPost> context)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CreatorEmail"] = context.Message.UserEmail,
                   ["CorrelationId"] = context.CorrelationId?.ToString() ?? Guid.NewGuid().ToString()
               }))
        {
            try
            {
                _logger.LogInformation("Starting post read information");
                var user = await _userManager.FindByEmailAsync(context.Message.UserEmail);
                if (user == null)
                {
                    _logger.LogWarning("User not found in system.");
                    await context.RespondAsync<OperationResponse<PostDto>>(new()
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.NotFound,
                        Message = "The user does not exist"
                    });
                    return;
                }

                _logger.LogDebug("Retrieved user {UserId}", user.Id);
                if (context.Message.Joined != null)
                {
                    var joined = context.Message.Joined;
                    if (joined.Value)
                    {
                        var postsWhereMember = await _db.Posts
                            .Where(p => p.Members.Any(m => m.Id == user.Id))
                            .Include(p => p.Members)
                            .ProjectToType<PostDto>()
                            .ToListAsync();
                        _logger.LogInformation("Retrieved joined posts {Count}", postsWhereMember.Count);
                        await context.RespondAsync<OperationResponse<PostDto>>(new()
                        {
                            Success = true,
                            StatusCode = HttpStatusCode.OK,
                            Message = "Successfully retrieved joined posts"
                        });
                        return;
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
                    _logger.LogInformation("Retrieved posts {UserPostsCount}, {RestPostsCount}", yourPosts.Count,
                        restPosts.Count);
                    var posts = new
                    {
                        yourPosts,
                        restPosts
                    };
                    await context.RespondAsync<OperationResponse<object>>(new()
                    {
                        Success = true,
                        StatusCode = HttpStatusCode.OK,
                        Data = posts,
                        Message = "Successfully retrieved posts"
                    });
                }
                else
                {
                    var postId = context.Message.PostId;
                    if (postId == null)
                    {
                        _logger.LogWarning("Invalid post ID. The value is can't be empty");
                        await context.RespondAsync<OperationResponse<PostDto>>(new()
                        {
                            Success = false,
                            StatusCode = HttpStatusCode.BadRequest,
                            Message = "The post ID value can't be null"
                        });
                        return;
                    }

                    var post = await _db.Posts
                        .Include(p => p.Members)
                        .Include(p => p.Creator)
                        .FirstOrDefaultAsync(p => p.Id == postId);
                    if (post == null)
                    {
                        _logger.LogWarning("The post was not found {PostId}", postId);
                        await context.RespondAsync<OperationResponse<PostDto>>(new()
                        {
                            Success = false,
                            StatusCode = HttpStatusCode.BadRequest,
                            Message = "The post with specified ID was not found"
                        });
                        return;
                    }

                    _logger.LogDebug("Successfully retrieved post {PostId}", postId);
                    var postDto = post.Adapt<PostDto>();
                    await context.RespondAsync<OperationResponse<PostDto>>(new()
                    {
                        Success = true,
                        StatusCode = HttpStatusCode.OK,
                        Data = postDto,
                        Message = "Successfully retrieved the post"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error processing post update");
                await context.RespondAsync<OperationResponse<PostDto>>(new()
                {
                    Success = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = "The error occured while processing the post"
                });
            }
        }
    }
}