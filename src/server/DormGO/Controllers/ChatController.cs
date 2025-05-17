using DormGO.Constants;
using DormGO.Data;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Filters;
using DormGO.Hubs;
using DormGO.Models;
using DormGO.Services;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DormGO.Controllers;
[Authorize]
[ApiController]
[ServiceFilter<ValidateUserEmailFilter>]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly ApplicationContext _db;
    private readonly IHubContext<ChatHub> _hub;
    private readonly ILogger<ChatController> _logger;
    private readonly IInputSanitizer _inputSanitizer;
    private readonly IMapper _mapper;

    public ChatController(ApplicationContext db, IHubContext<ChatHub> hub, ILogger<ChatController> logger,
        IInputSanitizer inputSanitizer, IMapper mapper)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
        _inputSanitizer = inputSanitizer;
        _mapper = mapper;
    }

    [HttpGet("{postId}/messages")]
    public async Task<IActionResult> GetMessagesForPost(string postId)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        if (string.IsNullOrWhiteSpace(postId))
        {
            _logger.LogWarning("Post id not provided during messages read for post. UserId: {UserId}", user.Id);
            return BadRequest(new { Message = "The post ID is invalid." });
        }
        var postExists = await _db.Posts.AnyAsync(p => p.Id == postId);
        if (!postExists)
        {
            var sanitizedPostId = _inputSanitizer.Sanitize(postId);
            _logger.LogWarning("Messages read for post requested for non-existent post. UserId: {UserId}, PostId: {PostId}", user.Id, sanitizedPostId);
            return NotFound(new { Message = "The post does not exist." });
        }
        
        var messages = await _db.Messages
            .Where(m => m.PostId == postId)
            .OrderBy(m => m.SentAt)
            .Include(m => m.Sender)
            .ProjectToType<MessageResponseDto>()
            .ToListAsync();
        _logger.LogInformation("Messages read for post successfully. UserId: {UserId}, MessagesCount: {MessageCount}", user.Id, messages.Count);
        return Ok(messages);
    }

    [HttpPost("{postId}/messages")]
    public async Task<IActionResult> AddMessageToPost(string postId, [FromBody] MessageRequestDto messageDto)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        
        if (string.IsNullOrWhiteSpace(postId))
        {
            _logger.LogWarning("Post id not provided during message send for post. UserId: {UserId}", user.Id);
            return BadRequest(new { Message = "The post ID is invalid." });
        }

        if (string.IsNullOrWhiteSpace(messageDto.Content))
        {
            _logger.LogWarning("Message content not provided during messages read for post. UserId: {UserId}", user.Id);
            return BadRequest(new { Message = "Message content cannot be null or empty." });
        }
        var post = await _db.Posts
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == postId);
        if (post == null)
        {
            var sanitizedPostId = _inputSanitizer.Sanitize(postId);
            _logger.LogWarning("Message send requested for non-existent post. UserId: {UserId}, PostId: {PostId}", user.Id, postId);
            return NotFound(new { Message = "The post does not exist." });
        }
        var message = _mapper.Map<Message>(messageDto);
        message.SenderId = user.Id;
        message.PostId = postId;
        _db.Messages.Add(message);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Message sent successfully. UserId: {UserId}, PostId: {PostId}, MessageId: {MessageId}", user.Id, postId, message.Id);
        var excludedConnectionIds = await _db.UserConnections
            .Where(uc => uc.UserId == user.Id && uc.Hub == "/api/chathub")
            .Select(uc => uc.ConnectionId)
            .ToListAsync();
        var responseDto = _mapper.Map<MessageResponseDto>(message);
        await _hub.Clients.GroupExcept(postId, excludedConnectionIds).SendAsync("ReceiveMessage", postId, responseDto);
        _logger.LogInformation(
            "Message on successful message delivery sent to users on hub. UserId: {UserId}, MessageId: {MessageId}, ExcludedConnectionIdCount: {ExcludedConnectionIdCount}",
            user.Id, message.Id, excludedConnectionIds.Count);
        return Ok(responseDto);
    }

    [HttpPut("{postId}/messages/{messageId}")]
    public async Task<IActionResult> UpdateMessage(string postId, string messageId, [FromBody] MessageRequestDto messageRequestDto)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        var message = await _db.Messages.FirstOrDefaultAsync(m => m.Id == messageId && m.PostId == postId);
        var sanitizedMessageId = _inputSanitizer.Sanitize(messageId);
        if (message == null)
        {
            _logger.LogWarning("Message update requested for non-existent message. UserId: {UserId}, MessageId: {MessageId}", user.Id, sanitizedMessageId);
            return NotFound(new { Message = "Message not found." });
        }

        if (message.SenderId != user.Id)
        {
            _logger.LogWarning("Message update requested by user who is not sender of message. UserId: {UserId}, MessageId: {MessageId}", user.Id, message.Id);
            return Forbid();
        }
        message.UpdatedAt = DateTime.UtcNow;
        message.Content = messageRequestDto.Content;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Message updated successfully. UserId: {UserId}. MessageId: {MessageId}", user.Id, sanitizedMessageId);
        Log.Information("UpdateMessage: Message {MessageId} updated by user {UserId}", message.Id, user.Id);
        var excludedConnectionIds = await _db.UserConnections
            .Where(uc => uc.UserId == user.Id && uc.Hub == "/api/chathub")
            .Select(uc => uc.ConnectionId)
            .ToListAsync();
        var responseDto = _mapper.Map<MessageResponseDto>(message);
        await _hub.Clients.GroupExcept(postId, excludedConnectionIds).SendAsync("UpdateMessage", postId, messageId, responseDto);
        _logger.LogInformation(
            "Message on successful message update sent to users on hub. UserId: {UserId}, MessageId: {MessageId}, ExcludedConnectionIdCount: {ExcludedConnectionIdCount}",
            user.Id, message.Id, excludedConnectionIds.Count);
        return Ok(new { Message = "The message was successfully updated." });
    }
    [HttpDelete("{postId}/messages/{messageId}")]
    public async Task<IActionResult> DeleteMessage(string postId, string messageId)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        var message = await _db.Messages.FirstOrDefaultAsync(m => m.Id == messageId && m.PostId == postId);
        var sanitizedMessageId = _inputSanitizer.Sanitize(messageId);
        if (message == null)
        {
            _logger.LogWarning("Message remove requested for non-existent message. UserId: {UserId}, MessageId: {MessageId}", user.Id, sanitizedMessageId);
            return NotFound(new { Message = "Message not found." });
        }
        if (message.SenderId != user.Id)
        {
            _logger.LogWarning("Message remove requested by user who is not the sender of message. UserId: {UserId}, MessageId: {MessageId}", user.Id, message.Id);
            return Forbid();
        }
        _db.Messages.Remove(message);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Message removed successfully. UserId: {UserId}. MessageId: {MessageId}", user.Id, message.Id);
        var excludedConnectionIds = await _db.UserConnections
            .Where(uc => uc.UserId == user.Id && uc.Hub == "/api/chathub")
            .Select(uc => uc.ConnectionId)
            .ToListAsync();
        await _hub.Clients.GroupExcept(postId, excludedConnectionIds).SendAsync("DeleteMessage", postId, messageId);
        _logger.LogInformation(
            "Message on successful message deletion sent to users on hub. UserId: {UserId}, MessageId: {MessageId}, ExcludedConnectionIdCount: {ExcludedConnectionIdCount}",
            user.Id, message.Id, excludedConnectionIds.Count);
        return Ok(new { Message = "The message was successfully removed" });
    }
}
