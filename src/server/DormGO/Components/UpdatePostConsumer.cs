using System.Net;
using DormGO.Contracts;
using DormGO.Data;
using DormGO.DTOs;
using DormGO.Hubs;
using DormGO.Models;
using DormGO.Services;
using Mapster;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DormGO.Components;

public class UpdatePostConsumer : IConsumer<UpdatePost>
{
    private readonly ApplicationContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UpdatePostConsumer> _logger;
    private readonly INotificationService _notificationService;
    private readonly IHubContext<PostHub> _hub;
    public UpdatePostConsumer(ApplicationContext db, UserManager<ApplicationUser> userManager, ILogger<UpdatePostConsumer> logger, INotificationService notificationService, IHubContext<PostHub> hub)
    {
        _db = db;
        _userManager = userManager;
        _notificationService = notificationService;
        _hub = hub; 
        _logger = logger;
    }
    public async Task Consume(ConsumeContext<UpdatePost> context)
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
                _logger.LogInformation("Starting post update information");
                var updatePostDto = context.Message.Post;
                var postToUpdate = await _db.Posts
                    .Include(p => p.Members)
                    .Include(p => p.Creator)
                    .FirstOrDefaultAsync(p => p.Id == postId);
                var user = await _userManager.FindByEmailAsync(userEmail);
                if (user == null)
                {
                    _logger.LogWarning("User not found in system");
                    await context.RespondAsync<OperationResponse<UserDto>>(new()
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.Unauthorized,
                        Message = "The user does not exist",
                    });
                    return;
                }
                _logger.LogDebug("Retrieved user {UserId}", user.Id);
                if (postToUpdate == null)
                {
                    _logger.LogWarning("Post not found in database");
                    await context.RespondAsync<OperationResponse<PostDto>>(new()
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.NotFound,
                        Message = "The post with the specified ID was not found.",
                    });
                    return;
                }

                if (postToUpdate.CreatorId != user.Id)
                {
                    _logger.LogWarning("Unauthorized update attempt by {UserId}", user.Id);
                    await context.RespondAsync<OperationResponse<PostDto>>(new()
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.Unauthorized,
                        Message = "You are not authorized to update the post"
                    });
                }

                var creator = postToUpdate.Creator;
                updatePostDto.Adapt(postToUpdate);
                postToUpdate.Creator = creator;
                if (updatePostDto.MembersToRemove.Any())
                {
                    var memberEmails = updatePostDto.MembersToRemove.Select(m => m.Email).ToList();
                    var users = await _db.Users
                        .Where(u => memberEmails.Contains(u.Email))
                        .ToListAsync();
                    if (users.Any())
                    {
                        var notification = new NotificationDto()
                        {
                            Message = $"You have been removed from the post: {postToUpdate.Title}",
                            Post = postToUpdate.Adapt<PostDto>()
                        };
                        foreach (var removedUser in users)
                        {
                            await _notificationService.NotifyUserAsync(removedUser.Id, notification);
                        }

                        _logger.LogInformation("Removing {Count} members", users.Count);
                        postToUpdate.Members = postToUpdate.Members.Except(users).ToList();
                    }
                }

                postToUpdate.UpdatedAt = DateTime.UtcNow;
                _db.Posts.Update(postToUpdate);
                await _db.SaveChangesAsync();
                _logger.LogDebug("Post updated successfully. Changes saved to database");
                var updatedPostDto = postToUpdate.Adapt<PostDto>();
                await _hub.Clients.All.SendAsync("PostUpdated", updatedPostDto);
                await context.RespondAsync<OperationResponse<PostDto>>(new()
                {
                    Success = true,
                    StatusCode = HttpStatusCode.OK,
                    Message = "Successfully updated the post",
                    Data = updatedPostDto
                });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error updating post {PostId}", postId);
                await context.RespondAsync<OperationResponse<PostDto>>(new()
                {
                    Success = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = "The error occured while updating the post"
                });
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