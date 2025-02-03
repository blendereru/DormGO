using System.Security.Claims;
using DormGO.Data;
using DormGO.DTOs;
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
        if (string.IsNullOrWhiteSpace(postId))
        {
            Log.Warning("GetMessagesForPost: Invalid postId provided.");
            return BadRequest(new { Message = "The post ID is invalid." });
        }

        var emailClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
        if (emailClaim == null)
        {
            Log.Warning("GetMessagesForPost: Email claim not found.");
            return Unauthorized(new { Message = "The email claim is not found." });
        }

        var user = await _userManager.FindByEmailAsync(emailClaim.Value);
        if (user == null)
        {
            Log.Warning("GetMessagesForPost: User not found with email: {Email}", emailClaim.Value);
            return NotFound(new { Message = "The user is not found." });
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
            .ProjectToType<MessageDto>()
            .ToListAsync();

        return Ok(messages);
    }

    [HttpPost("{postId}/messages")]
    public async Task<IActionResult> AddMessageToPost(string postId, [FromBody] MessageDto messageDto)
    {
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

        var emailClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(emailClaim))
        {
            Log.Warning("AddMessageToPost: Unauthorized access attempt. Email claim missing.");
            return Unauthorized(new { Message = "The email claim is missing from the token." });
        }

        var user = await _userManager.FindByEmailAsync(emailClaim);
        if (user == null)
        {
            Log.Warning("AddMessageToPost: User not found with email: {Email}", emailClaim);
            return NotFound(new { Message = "The user with the provided email is not found." });
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
        var responseDto = _mapper.Map<MessageDto>(message);
        await _hub.Clients.GroupExcept(postId, excludedConnectionIds).SendAsync("ReceiveMessage", postId, responseDto);
        return Ok(responseDto);
    }

    [HttpDelete("messages/{messageId}")]
    public async Task<IActionResult> DeleteMessage(string messageId)
    {
        var emailClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(emailClaim))
        {
            Log.Warning("DeleteMessage: Unauthorized access attempt. Email claim missing.");
            return Unauthorized(new { Message = "The email claim is missing from the token." });
        }

        var user = await _userManager.FindByEmailAsync(emailClaim);
        if (user == null)
        {
            Log.Warning("DeleteMessage: User not found with email: {Email}", emailClaim);
            return NotFound(new { Message = "The user with the provided email is not found." });
        }

        var message = await _db.Messages
            .Include(m => m.Sender)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null)
        {
            Log.Warning("DeleteMessage: Message not found with messageId: {MessageId}", messageId);
            return NotFound(new { Message = "Message not found." });
        }
        if (message.SenderId != user.Id)
        {
            Log.Warning("DeleteMessage: Unauthorized attempt to delete message. UserId: {UserId}, MessageId: {MessageId}", user.Id, messageId);
            return BadRequest(new { Message = "You are not authorized to delete this message." });
        }
        _db.Messages.Remove(message);
        await _db.SaveChangesAsync();
        Log.Information("User {UserId} deleted Message {MessageId}.", user.Id, messageId);
        return Ok(new { Message = "The message was successfully removed" });
    }
}
