using DormGO.Constants;
using DormGO.Data;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Filters;
using DormGO.Hubs;
using DormGO.Models;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<ChatHub> _hub;
    private readonly IMapper _mapper;

    public ChatController(ApplicationContext db, IMapper mapper, 
        UserManager<ApplicationUser> userManager, IHubContext<ChatHub> hub)
    {
        _db = db;
        _userManager = userManager;
        _hub = hub;
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
            Log.Warning("GetMessagesForPost: Invalid postId provided.");
            return BadRequest(new { Message = "The post ID is invalid." });
        }
        var postExists = await _db.Posts.AnyAsync(p => p.Id == postId);
        if (!postExists)
        {
            Log.Warning("GetMessagesForPost: Post not found with postId: {PostId}", postId);
            return NotFound(new { Message = "The post does not exist." });
        }

        Log.Information("User {UserId} is retrieving messages for Post {PostId}.", user.Id, postId);

        var messages = await _db.Messages
            .Where(m => m.PostId == postId)
            .OrderBy(m => m.SentAt)
            .Include(m => m.Sender)
            .ProjectToType<MessageResponseDto>()
            .ToListAsync();

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
            Log.Warning("AddMessageToPost: Invalid postId provided.");
            return BadRequest(new { Message = "The post ID is invalid." });
        }

        if (string.IsNullOrWhiteSpace(messageDto.Content))
        {
            Log.Warning("AddMessageToPost: Invalid message content.");
            return BadRequest(new { Message = "Message content cannot be null or empty." });
        }
        var post = await _db.Posts
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == postId);
        if (post == null)
        {
            Log.Warning("AddMessageToPost: Post not found with postId: {PostId}", postId);
            return NotFound(new { Message = "The post does not exist." });
        }
        var message = _mapper.Map<Message>(messageDto);
        message.SenderId = user.Id;
        message.PostId = postId;
        _db.Messages.Add(message);
        await _db.SaveChangesAsync();
        Log.Information("User {UserId} added a message to Post {PostId}. MessageId: {MessageId}", user.Id, postId, message.Id);
        var excludedConnectionIds = await _db.UserConnections
            .Where(uc => uc.UserId == user.Id && uc.Hub == "/api/chathub")
            .Select(uc => uc.ConnectionId)
            .ToListAsync();
        var responseDto = _mapper.Map<MessageResponseDto>(message);
        await _hub.Clients.GroupExcept(postId, excludedConnectionIds).SendAsync("ReceiveMessage", postId, responseDto);
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
        if (message == null)
        {
            Log.Warning("UpdateMessage: Message not found with messageId: {MessageId} and postId: {PostId}", messageId, postId);
            return NotFound(new { Message = "Message not found." });
        }

        if (message.SenderId != user.Id)
        {
            Log.Warning("UpdateMessage: Unauthorized attempt to update message. UserId: {UserId}, MessageId: {MessageId}, PostId: {PostId}", user.Id, messageId, postId);
            return Forbid();
        }

        message.UpdatedAt = DateTime.UtcNow;
        message.Content = messageRequestDto.Content;
        await _db.SaveChangesAsync();
        Log.Information("UpdateMessage: Message {MessageId} updated by user {UserId} in post {PostId}", message.Id, user.Id, postId);
        var excludedConnectionIds = await _db.UserConnections
            .Where(uc => uc.UserId == user.Id && uc.Hub == "/api/chathub")
            .Select(uc => uc.ConnectionId)
            .ToListAsync();
        var responseDto = _mapper.Map<MessageResponseDto>(message);
        await _hub.Clients.GroupExcept(postId, excludedConnectionIds).SendAsync("UpdateMessage", postId, messageId, responseDto);
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
        if (message == null)
        {
            Log.Warning("DeleteMessage: Message not found with messageId: {MessageId}", messageId);
            return NotFound(new { Message = "Message not found." });
        }
        if (message.SenderId != user.Id)
        {
            Log.Warning("DeleteMessage: Unauthorized attempt to delete message. UserId: {UserId}, MessageId: {MessageId}", user.Id, messageId);
            return Forbid();
        }
        _db.Messages.Remove(message);
        await _db.SaveChangesAsync();
        Log.Information("User {UserId} deleted Message {MessageId}.", user.Id, messageId);
        var excludedConnectionIds = await _db.UserConnections
            .Where(uc => uc.UserId == user.Id && uc.Hub == "/api/chathub")
            .Select(uc => uc.ConnectionId)
            .ToListAsync();
        await _hub.Clients.GroupExcept(postId, excludedConnectionIds).SendAsync("DeleteMessage", postId, messageId);
        return Ok(new { Message = "The message was successfully removed" });
    }
}
