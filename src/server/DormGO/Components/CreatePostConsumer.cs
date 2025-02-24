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

public class CreatePostConsumer : IConsumer<CreatePost>
{
    private readonly ApplicationContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<PostHub> _hub;
    private readonly ILogger<CreatePostConsumer> _logger;

    public CreatePostConsumer(ApplicationContext db, UserManager<ApplicationUser> userManager, IHubContext<PostHub> hub, ILogger<CreatePostConsumer> logger)
    {
        _db = db;
        _userManager = userManager;
        _hub = hub;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CreatePost> context)
    {
        var creatorEmail = context.Message.CreatorEmail;
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CreatorEmail"] = creatorEmail,
                   ["CorrelationId"] = context.CorrelationId?.ToString() ?? Guid.NewGuid().ToString()
               }))
        {
            try
            {
                _logger.LogInformation("Starting post creation process.");
                var creator = await _userManager.FindByEmailAsync(creatorEmail);
                if (creator == null)
                {
                    _logger.LogWarning("User not found in system.");
                    await context.RespondAsync<OperationResponse<PostDto>>(new()
                    {
                        Success = false,
                        StatusCode = HttpStatusCode.NotFound,
                        Message = "Invalid creator email"
                    });
                    return;
                }
                _logger.LogDebug("Retrieved user {UserId}.", creator.Id);
                var connectionIds = await _db.UserConnections
                    .Where(c => c.UserId == creator.Id && c.Hub == "/api/posthub")
                    .Select(uc => uc.ConnectionId)
                    .ToListAsync();
                var post = context.Message.Post.Adapt<Post>();
                post.CreatorId = creator.Id;
                _db.Posts.Add(post);
                await _db.SaveChangesAsync();
                _logger.LogInformation("Post created successfully with ID {PostId}.", post.Id);
                var postDto = post.Adapt<PostDto>();
                await _hub.Clients.User(creator.Id).SendAsync("PostCreated", true, postDto);
                await _hub.Clients.AllExcept(connectionIds).SendAsync("PostCreated", false, postDto);
                await context.RespondAsync<OperationResponse<PostDto>>(new()
                {
                    Success = true,
                    StatusCode = HttpStatusCode.Created,
                    Data = postDto
                });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error occurred while creating post.");
                await context.RespondAsync<OperationResponse<PostDto>>(new()
                {
                    Success = false,
                    StatusCode = HttpStatusCode.InternalServerError,
                    Message = "An error occurred while creating the post."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error occurred during post creation process.");
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
