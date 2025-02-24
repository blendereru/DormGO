using System.Net;
using DormGO.Contracts;
using DormGO.Data;
using DormGO.DTOs;
using DormGO.Hubs;
using DormGO.Models;
using Mapster;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DormGO.Components;

public class DeletePostConsumer : IConsumer<DeletePost>
{
    private readonly ApplicationContext _db;
    private readonly IHubContext<PostHub> _hub;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DeletePostConsumer> _logger;

    public DeletePostConsumer(ApplicationContext db, IHubContext<PostHub> hub, UserManager<ApplicationUser> userManager,
        ILogger<DeletePostConsumer> logger)
    {
        _db = db;
        _userManager = userManager;
        _hub = hub;
        _logger = logger;
    }
    public async Task Consume(ConsumeContext<DeletePost> context)
    {
        var postId = context.Message.PostId;
        var userEmail = context.Message.UserEmail;
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["PostId"] = postId,
                   ["UserEmail"] = userEmail,
                   ["CorrelationId"] = context.CorrelationId?.ToString() ?? Guid.NewGuid().ToString()
               }))
        {
            try
            {
                _logger.LogInformation("Starting post deletion information");
                var user = await _userManager.FindByEmailAsync(userEmail);
                if (user == null)
                {
                    _logger.LogWarning("The user was not found");
                    await context.RespondAsync<OperationResponse<PostDto>>(new()
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.NotFound,
                        Message = "The user with specified ID was not found"
                    });
                    return;
                }
                _logger.LogDebug("Retrieved user {UserId}", user.Id);
                var post = await _db.Posts
                    .Include(p => p.Members)
                    .FirstOrDefaultAsync(p => p.Id == postId);
                if (post == null)
                {
                    _logger.LogWarning("Post was not found");
                    await context.RespondAsync<OperationResponse<PostDto>>(new()
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.NotFound,
                        Message = "The post with specified ID was not found"
                    });
                    return;
                }
                _logger.LogDebug("Successfully retrieved the post");
                if (post.CreatorId != user.Id)
                {
                    _logger.LogWarning("Unauthorized attempt to delete the post");
                    await context.RespondAsync<OperationResponse<PostDto>>(new()
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.Unauthorized,
                        Message = "You are not authorized to delete this post"
                    });
                    return;
                }

                var postDto = post.Adapt<PostDto>();
                _db.Posts.Remove(post);
                await _db.SaveChangesAsync();
                _logger.LogInformation("Successful deletion of post. Changes saved to database");
                await _hub.Clients.All.SendAsync("PostDeleted", post.Id);
                await context.RespondAsync<OperationResponse<PostDto>>(new()
                {
                    Success = true,
                    StatusCode = HttpStatusCode.OK,
                    Message = "The post was successfully removed",
                    Data = postDto
                });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error deleting post {PostId}", postId);
                await context.RespondAsync<OperationResponse<PostDto>>(new()
                {
                    Success = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = "The error occured while deleting the post"
                });
            }
            catch (HubException hubEx)
            {
                _logger.LogError(hubEx, "Hub error while deleting the post {PostId}", postId);
                await context.RespondAsync<OperationResponse<PostDto>>(new()
                {
                    Success = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = "The error occured while deleting the post"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error occurred during post deleting process.");
                await context.RespondAsync<OperationResponse<PostDto>>(new()
                {
                    Success = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = "An unexpected error occurred."
                });
            } 
        }
    }
}