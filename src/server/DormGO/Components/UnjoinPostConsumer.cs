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

public class UnjoinPostConsumer : IConsumer<UnjoinPost>
{
    private readonly ApplicationContext _db;
    private readonly ILogger<UnjoinPostConsumer> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<PostHub> _hub;
    public UnjoinPostConsumer(ApplicationContext db, UserManager<ApplicationUser> userManager,
        IHubContext<PostHub> hub, ILogger<UnjoinPostConsumer> logger)
    {
        _db = db;
        _userManager = userManager;
        _hub = hub;
        _logger = logger;
    }
    public async Task Consume(ConsumeContext<UnjoinPost> context)
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
                _logger.LogInformation("Starting post unjoin information");
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
                if (post.Members.Any(m => m.Id == user.Id))
                {
                    _logger.LogWarning("Invalid attempt to unjoin from the post where user is not a member.");
                    await context.RespondAsync<OperationResponse<PostDto>>(new()
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.BadRequest,
                        Message = "The user is not a member of the post."
                    });
                    return;
                }

                if (post.CreatorId == user.Id)
                {
                    if (post.Members.Count == 1)
                    {
                        _db.Posts.Remove(post);
                        await _db.SaveChangesAsync();
                        _logger.LogInformation("Post unjoined successfully. Changes saved to database");
                        _logger.LogInformation("Deleting the post as there is no member left");
                        await context.RespondAsync<OperationResponse<PostDto>>(new()
                        {
                            Success = true,
                            StatusCode = HttpStatusCode.OK,
                            Message = "Successfully unjoined the post. Post was removed as there was no member left"
                        });
                        return;
                    }
                    var newCreator = post.Members.FirstOrDefault(m => m.Id != user.Id);
                    if (newCreator != null)
                    {
                        post.CreatorId = newCreator.Id;
                        _logger.LogInformation("Ownership of post transferred from User {OldCreatorId} to new {NewCreatorId}", user.Id, newCreator.Id);
                    }
                }

                post.Members.Remove(user);
                await _db.SaveChangesAsync();
                var updatedPostDto = post.Adapt<PostDto>();
                await _hub.Clients.All.SendAsync("PostUnjoined", updatedPostDto);
                _logger.LogInformation("Successful removal of user {UserId} from post", user.Id);
                await context.RespondAsync<OperationResponse<PostDto>>(new()
                {
                    Success = true,
                    StatusCode = HttpStatusCode.OK,
                    Message = "You were successfully removed from the post's members"
                });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error unjoining post {PostId}", postId);
                await context.RespondAsync<OperationResponse<PostDto>>(new()
                {
                    Success = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = "The error occured while unjoining the post"
                });
            }
            catch (HubException hubEx)
            {
                _logger.LogError(hubEx, "Hub error while unjoining the post {PostId}", postId);
                await context.RespondAsync<OperationResponse<PostDto>>(new()
                {
                    Success = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = "The error occured while unjoining the post"
                });
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Critical error occurred during post unjoining process.");
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