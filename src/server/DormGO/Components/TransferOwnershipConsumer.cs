using System.Net;
using DormGO.Contracts;
using DormGO.Data;
using DormGO.DTOs;
using DormGO.Models;
using DormGO.Services;
using Mapster;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DormGO.Components;

public class TransferOwnershipConsumer : IConsumer<TransferOwnership>
{
    private readonly ApplicationContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<TransferOwnershipConsumer> _logger;
    private readonly INotificationService _notificationService;

    public TransferOwnershipConsumer(ApplicationContext db, UserManager<ApplicationUser> userManager,
        INotificationService notificationService, ILogger<TransferOwnershipConsumer> logger)
    {
        _db = db;
        _userManager = userManager;
        _notificationService = notificationService;
        _logger = logger;
    }
    public async Task Consume(ConsumeContext<TransferOwnership> context)
    {
        var postId = context.Message.PostId;
        var userEmail = context.Message.UserEmail;
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["PostId"] = postId,
                   ["UserEmail"] = userEmail,
                   ["NewOwnerEmail"] = context.Message.NewOwner.Email,
                   ["CorrelationId"] = context.CorrelationId?.ToString() ?? Guid.NewGuid().ToString()
               }))
        {
            try
            {
                _logger.LogInformation("Starting transfer ownership information");
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
                    _logger.LogWarning("Unauthorized attempt to transfer the ownership");
                    await context.RespondAsync<OperationResponse<PostDto>>(new()
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.Unauthorized,
                        Message = "You are not authorized to delete this post"
                    });
                    return;
                }

                var newOwner = post.Members.FirstOrDefault(m => m.Email == context.Message.NewOwner.Email);
                if (newOwner == null)
                {
                    _logger.LogWarning("Invalid attempt to transfer the ownership to the user who is not a member of the post");
                    await context.RespondAsync<OperationResponse<PostDto>>(new()
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.NotFound,
                        Message = "The new owner must be a member of the post"
                    });
                    return;
                }
                post.CreatorId = newOwner.Id;
                await _db.SaveChangesAsync();
                _logger.LogInformation("Successful ownership transfer. Changes saved to database");
                var notification = new NotificationDto()
                {
                    Message = $"You are now the member of the post: {post.Title}",
                    Post = post.Adapt<PostDto>()
                };
                await _notificationService.NotifyUserAsync(newOwner.Id, notification);
                _logger.LogInformation("Post's old creator: {OldCreator}", user.Id);
                await context.RespondAsync<OperationResponse<PostDto>>(new()
                {
                    Success = true,
                    StatusCode = HttpStatusCode.OK,
                    Message = "Ownership transferred successfully",
                    Data = post.Adapt<PostDto>()
                });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error transferring ownership post {PostId}", postId);
                await context.RespondAsync<OperationResponse<PostDto>>(new()
                {
                    Success = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = "The error occured while transferring the ownership"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error occurred during ownership transfer process.");
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