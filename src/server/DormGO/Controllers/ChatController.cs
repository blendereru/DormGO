using DormGO.Constants;
using DormGO.Data;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Filters;
using DormGO.Models;
using DormGO.Services;
using DormGO.Services.Notifications;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DormGO.Controllers;
[Authorize]
[ApiController]
[ServiceFilter<ValidateUserEmailFilter>]
[Route("api/chat/{postId}/messages")]
public class ChatController : ControllerBase
{
    private readonly ApplicationContext _db;
    private readonly IChatNotificationService _chatNotificationService;
    private readonly ILogger<ChatController> _logger;
    private readonly IInputSanitizer _inputSanitizer;
    private readonly IMapper _mapper;

    public ChatController(ApplicationContext db, IChatNotificationService chatNotificationService, ILogger<ChatController> logger,
        IInputSanitizer inputSanitizer, IMapper mapper)
    {
        _db = db;
        _chatNotificationService = chatNotificationService;
        _logger = logger;
        _inputSanitizer = inputSanitizer;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<IActionResult> GetMessagesForPost(string postId)
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
                Title = "Not Found",
                Detail = "The post does not exist.",
                Status = StatusCodes.Status404NotFound,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        var messages = await _db.Messages
            .Where(m => m.PostId == postId)
            .OrderBy(m => m.SentAt)
            .Include(m => m.Sender)
            .ProjectToType<MessageResponseDto>()
            .ToListAsync();
        _logger.LogInformation("Messages retrieved for post successfully. UserId: {UserId}, MessagesCount: {MessageCount}", user.Id, messages.Count);
        return Ok(messages);
    }

    [HttpPost]
    public async Task<IActionResult> AddMessageToPost(string postId, MessageRequestDto messageDto)
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

        if (string.IsNullOrWhiteSpace(messageDto.Content))
        {
            _logger.LogWarning("Message content not provided during message send for post. UserId: {UserId}", user.Id);
            ModelState.AddModelError(nameof(messageDto.Content), "Message content cannot be null or empty.");
            return ValidationProblem(ModelState);
        }
        var sanitizedPostId = _inputSanitizer.Sanitize(postId);
        var post = await _db.Posts
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == sanitizedPostId);
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
        var message = _mapper.Map<Message>(messageDto);
        message.SenderId = user.Id;
        message.PostId = sanitizedPostId;
        _db.Messages.Add(message);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Message sent successfully. UserId: {UserId}, PostId: {PostId}, MessageId: {MessageId}", user.Id, post.Id, message.Id);
        await _chatNotificationService.NotifyMessageSentAsync(user, message.Adapt<MessageResponseDto>());
        return CreatedAtAction("GetMessageById", new { postId = message.PostId, messageId = message.Id }, message.Adapt<MessageResponseDto>());
    }

    [HttpGet("{messageId}")]
    public async Task<IActionResult> GetMessageById(string postId, string messageId)
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
        var responseDto = message.Adapt<MessageResponseDto>();
        _logger.LogInformation("Message retrieved successfully. UserId: {UserId}, MessageId: {MessageId}", user.Id, message.Id);
        return Ok(responseDto);
    }
    [HttpPut("{messageId}")]
    public async Task<IActionResult> UpdateMessage(string postId, string messageId, MessageRequestDto messageRequestDto)
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
        if (string.IsNullOrWhiteSpace(messageRequestDto.Content))
        {
            _logger.LogWarning("Message content not provided during message update. UserId: {UserId}", user.Id);
            ModelState.AddModelError(nameof(messageRequestDto.Content), "Message content cannot be null or empty.");
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
        message.UpdatedAt = DateTime.UtcNow;
        message.Content = messageRequestDto.Content;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Message updated successfully. UserId: {UserId}. MessageId: {MessageId}", user.Id, sanitizedMessageId);
        Log.Information("UpdateMessage: Message {MessageId} updated by user {UserId}", message.Id, user.Id);
        await _chatNotificationService.NotifyMessageUpdatedAsync(user, message.Adapt<MessageResponseDto>());
        return NoContent();
    }
    [HttpDelete("{messageId}")]
    public async Task<IActionResult> DeleteMessage(string postId, string messageId)
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
        await _chatNotificationService.NotifyMessageDeletedAsync(user, message.Adapt<MessageResponseDto>());
        return NoContent();
    }
}
