using System.ComponentModel;
using DormGO.Constants;
using DormGO.Data;
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
using Serilog;

namespace DormGO.Controllers;
[Authorize]
[ApiController]
[ServiceFilter<ValidateUserEmailFilter>]
[Route("api/chat/{postId}/messages")]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public class ChatController : ControllerBase
{
    private readonly ApplicationContext _db;
    private readonly IChatHubNotificationService _chatHubNotificationService;
    private readonly ILogger<ChatController> _logger;
    private readonly IInputSanitizer _inputSanitizer;

    public ChatController(ApplicationContext db, IChatHubNotificationService chatHubNotificationService,
        ILogger<ChatController> logger,
        IInputSanitizer inputSanitizer)
    {
        _db = db;
        _chatHubNotificationService = chatHubNotificationService;
        _logger = logger;
        _inputSanitizer = inputSanitizer;
    }
    [EndpointSummary("Retrieve messages for post")]
    [EndpointDescription("Retrieve all messages belonging to the same post")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<List<MessageResponse>>(StatusCodes.Status200OK, "application/json")]
    [HttpGet]
    public async Task<IActionResult> GetMessagesForPost([Description("Id of the post to retrieve the messages of")] string postId)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("Messages retrieve attempted with missing or invalid user context.");
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User information is missing.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        if (string.IsNullOrWhiteSpace(postId))
        {
            _logger.LogWarning("Post id not provided during messages retrieve for post. UserId: {UserId}", user.Id);
            ModelState.AddModelError(nameof(postId), "The postId parameter is required");
            return ValidationProblem(ModelState);
        }
        var sanitizedPostId = _inputSanitizer.Sanitize(postId);
        var postExists = await _db.Posts.AsNoTracking().AnyAsync(p => p.Id == sanitizedPostId);
        if (!postExists)
        {
            _logger.LogWarning("Messages retrieve requested for non-existent post. UserId: {UserId}, PostId: {PostId}", user.Id, sanitizedPostId);
            return NotFound(new ProblemDetails
            {
                Title = "Not found",
                Detail = "The post does not exist.",
                Status = StatusCodes.Status404NotFound,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        var messages = await _db.Messages
            .Where(m => m.PostId == sanitizedPostId)
            .OrderBy(m => m.SentAt)
            .Include(m => m.Sender)
            .ProjectToType<MessageResponse>()
            .ToListAsync();
        _logger.LogInformation("Messages retrieved for post successfully. UserId: {UserId}, MessagesCount: {MessageCount}", user.Id, messages.Count);
        return Ok(messages);
    }
    [EndpointSummary("Send message")]
    [EndpointDescription("Send a message to the users of the post")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created, "application/json")]
    [HttpPost]
    public async Task<IActionResult> AddMessageToPost([Description("The id of the post to send the message of")] string postId, MessageCreateRequest messageCreateRequest)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("Message send attempted with missing or invalid user context.");
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User information is missing.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        
        if (string.IsNullOrWhiteSpace(postId))
        {
            _logger.LogWarning("Post id not provided during message send for post. UserId: {UserId}", user.Id);
            ModelState.AddModelError(nameof(postId), "The postId parameter is required.");
            return ValidationProblem(ModelState);
        }
        var sanitizedPostId = _inputSanitizer.Sanitize(postId);
        var post = await _db.Posts.FirstOrDefaultAsync(p => p.Id == sanitizedPostId);
        if (post == null)
        {
            _logger.LogWarning("Message send requested for non-existent post. UserId: {UserId}, PostId: {PostId}", user.Id, sanitizedPostId);
            return NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = "The post does not exist.",
                Status = StatusCodes.Status404NotFound,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        var message = messageCreateRequest.Adapt<Message>();
        message.SenderId = user.Id;
        message.PostId = sanitizedPostId;
        _db.Messages.Add(message);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Message sent successfully. UserId: {UserId}, PostId: {PostId}, MessageId: {MessageId}", user.Id, post.Id, message.Id);
        await _chatHubNotificationService.NotifyMessageSentAsync(user, message);
        return CreatedAtAction("GetMessageById", new { postId = message.PostId, messageId = message.Id }, message.Adapt<MessageResponse>());
    }
    [EndpointSummary("Retrieve a message")]
    [EndpointDescription("Intended to retrieve a message belonging to the current user")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK, "application/json")]
    [HttpGet("{messageId}")]
    public async Task<IActionResult> GetMessageById([Description("Post id to retrieve the message of")] string postId, [Description("Id of the message to retrieve")] string messageId)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("Message retrieve attempted with missing or invalid user context.");
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User information is missing.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        if (string.IsNullOrWhiteSpace(postId))
        {
            _logger.LogWarning("Post id not provided during message retrieve. UserId: {UserId}", user.Id);
            ModelState.AddModelError(nameof(postId), "The postId parameter is required.");
            return ValidationProblem(ModelState);
        }
        if (string.IsNullOrWhiteSpace(messageId))
        {
            _logger.LogWarning("Message id not provided during message retrieve. UserId: {UserId}", user.Id);
            ModelState.AddModelError(nameof(messageId), "The messageId parameter is required.");
            return ValidationProblem(ModelState);
        }
        var sanitizedPostId = _inputSanitizer.Sanitize(postId);
        var sanitizedMessageId = _inputSanitizer.Sanitize(messageId);
        var message = await _db.Messages.FirstOrDefaultAsync(m =>
            m.Id == sanitizedMessageId &&
            m.PostId == sanitizedPostId &&
            m.SenderId == user.Id);
        if (message == null)
        {
            _logger.LogWarning("Message retrieve requested for non-existent or unauthorized message. UserId: {UserId}, MessageId: {MessageId}", user.Id, sanitizedMessageId);
            return NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = "Message not found.",
                Status = StatusCodes.Status404NotFound,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }

        var response = message.Adapt<MessageResponse>();
        _logger.LogInformation("Message retrieved successfully. UserId: {UserId}, MessageId: {MessageId}", user.Id, message.Id);
        return Ok(response);
    }
    [EndpointSummary("Update a message")]
    [EndpointDescription("Updates a message of the current user")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpPut("{messageId}")]
    public async Task<IActionResult> UpdateMessage([Description("Post id to update the message of")] string postId, [Description("Id of the message to update")] string messageId, MessageUpdateRequest messageUpdateRequest)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("Message update attempted with missing or invalid user context.");
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User information is missing.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        if (string.IsNullOrWhiteSpace(postId))
        {
            _logger.LogWarning("Post id not provided during message update. UserId: {UserId}", user.Id);
            ModelState.AddModelError(nameof(postId), "The postId parameter is required.");
            return ValidationProblem(ModelState);
        }
        if (string.IsNullOrWhiteSpace(messageId))
        {
            _logger.LogWarning("Message id not provided during message update. UserId: {UserId}", user.Id);
            ModelState.AddModelError(nameof(messageId), "The messageId parameter is required.");
            return ValidationProblem(ModelState);
        }
        var sanitizedPostId = _inputSanitizer.Sanitize(postId);
        var sanitizedMessageId = _inputSanitizer.Sanitize(messageId);
        var message = await _db.Messages.FirstOrDefaultAsync(m =>
            m.Id == sanitizedMessageId &&
            m.PostId == sanitizedPostId &&
            m.SenderId == user.Id);
        if (message == null)
        {
            _logger.LogWarning("Message update requested for non-existent or unauthorized message. UserId: {UserId}, MessageId: {MessageId}", user.Id, sanitizedMessageId);
            return NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = "Message not found.",
                Status = StatusCodes.Status404NotFound,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }

        if (message.Content == messageUpdateRequest.Content.Trim())
        {
            _logger.LogInformation("Message content didn't change during message update. UserId: {UserId}, MessageId: {MessageId}", user.Id, message.Id);
        }
        else
        {
            message.UpdatedAt = messageUpdateRequest.UpdatedAt;
            message.Content = messageUpdateRequest.Content;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Message updated successfully. UserId: {UserId}. MessageId: {MessageId}", user.Id, sanitizedMessageId);
            Log.Information("Message {MessageId} updated by user {UserId}", message.Id, user.Id);
            await _chatHubNotificationService.NotifyMessageUpdatedAsync(user, message);
        }
        return NoContent();
    }
    [EndpointSummary("Delete the message")]
    [EndpointDescription("Delete the message of the current user")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpDelete("{messageId}")]
    public async Task<IActionResult> DeleteMessage([Description("Post id to delete the message of")] string postId, [Description("Id of the message to delete")] string messageId)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("Message delete attempted with missing or invalid user context.");
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User information is missing.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        if (string.IsNullOrWhiteSpace(postId))
        {
            _logger.LogWarning("Post id not provided during message delete. UserId: {UserId}", user.Id);
            ModelState.AddModelError(nameof(postId), "The postId parameter is required.");
            return ValidationProblem(ModelState);
        }
        if (string.IsNullOrWhiteSpace(messageId))
        {
            _logger.LogWarning("Message id not provided during message delete. UserId: {UserId}", user.Id);
            ModelState.AddModelError(nameof(messageId), "The messageId parameter is required.");
            return ValidationProblem(ModelState);
        }
        var sanitizedPostId = _inputSanitizer.Sanitize(postId);
        var sanitizedMessageId = _inputSanitizer.Sanitize(messageId);
        var message = await _db.Messages.FirstOrDefaultAsync(m =>
            m.Id == sanitizedMessageId &&
            m.PostId == sanitizedPostId &&
            m.SenderId == user.Id);
        if (message == null)
        {
            _logger.LogWarning("Message delete requested for non-existent or unauthorized message. UserId: {UserId}, MessageId: {MessageId}", user.Id, sanitizedMessageId);
            return NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = "Message not found.",
                Status = StatusCodes.Status404NotFound,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        _db.Messages.Remove(message);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Message removed successfully. UserId: {UserId}. MessageId: {MessageId}", user.Id, message.Id);
        await _chatHubNotificationService.NotifyMessageDeletedAsync(user, message);
        return NoContent();
    }
}
