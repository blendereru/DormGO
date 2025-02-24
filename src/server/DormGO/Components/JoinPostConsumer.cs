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

public class JoinPostConsumer : IConsumer<JoinPost>
{
    private readonly ApplicationContext _db;
    private readonly IHubContext<PostHub> _hub;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<JoinPostConsumer> _logger;
    public JoinPostConsumer(ApplicationContext db, UserManager<ApplicationUser> userManager, IHubContext<PostHub> hub, ILogger<JoinPostConsumer> logger)
    {
        _db = db;
        _userManager = userManager;
        _hub = hub;
        _logger = logger;
    }
    public async Task Consume(ConsumeContext<JoinPost> context)
    {
        var userEmail = context.Message.UserEmail;
        var postId = context.Message.PostId;
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["UserEmail"] = userEmail,
                   ["PostId"] = postId,
                   ["CorrelationId"] = context.CorrelationId?.ToString() ?? Guid.NewGuid().ToString()
               }))
        {
            try
            {
                _logger.LogInformation("Starting post join information");
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
                if (post.CreatorId == user.Id)
                {
                    _logger.LogWarning("Invalid attempt of joining the post created by user himself {UserId}", user.Id);
                    await context.RespondAsync<OperationResponse<PostDto>>(new()
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.BadRequest,
                        Message = "You can't join the post as you are the creator of the post."
                    });
                    return;
                }

                if (post.Members.Any(m => m.Id == user.Id))
                {
                    _logger.LogWarning("Attempt to join the post where user is already a member {UserId}", user.Id);
                    await context.RespondAsync<OperationResponse<PostDto>>(new()
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.BadRequest,
                        Message = "You can't join the post as you are already a member of the post"
                    });
                    return;
                }

                if (post.Members.Count >= post.MaxPeople)
                {
                    _logger.LogWarning("The post hit the maximum number of people {MembersCount} / {MaxPeople}",
                        post.Members.Count, post.MaxPeople);
                    await context.RespondAsync<OperationResponse<PostDto>>(new()
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.BadRequest,
                        Message = "The post has reached its maximum member capacity"
                    });
                    return;
                }

                post.Members.Add(user);
                await _db.SaveChangesAsync();
                _logger.LogInformation("Post joined successfully. Changes saved to database");
                var updatedPostDto = post.Adapt<PostDto>();
                await _hub.Clients.All.SendAsync("PostJoined", updatedPostDto);
                await context.RespondAsync<OperationResponse<PostDto>>(new()
                {
                    Success = true,
                    StatusCode = HttpStatusCode.OK,
                    Message = "You were successfully added as the member of the post",
                    Data = updatedPostDto
                });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error joining post {PostId}", postId);
                await context.RespondAsync<OperationResponse<PostDto>>(new()
                {
                    Success = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = "The error occured while joining the post"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error occurred during post joining process.");
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