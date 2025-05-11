using System.Security.Claims;
using DormGO.Data;
using DormGO.DTOs.ResponseDTO;
using DormGO.Models;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DormGO.Hubs;
[Authorize]
public class ChatHub : Hub
{
    private readonly ApplicationContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMapper _mapper;
    public ChatHub(ApplicationContext db, UserManager<ApplicationUser> userManager, IMapper mapper)
    {
        _db = db;
        _userManager = userManager;
        _mapper = mapper;
    }
    public override async Task OnConnectedAsync()
    {
        try
        {
            var userId = Context?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                Log.Warning("Connection aborted: Missing or empty user ID. ConnectionId: {ConnectionId}", Context.ConnectionId);
                Context.Abort();
                return;
            }
            var ip = Context?.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrEmpty(ip))
            {
                Log.Warning("Connection aborted: Missing IP address. UserId: {UserId}, ConnectionId: {ConnectionId}", userId, Context.ConnectionId);
                Context.Abort();
                return;
            }
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                Log.Warning("Connection aborted: User not found in database. UserId: {UserId}, ConnectionId: {ConnectionId}", userId, Context.ConnectionId);
                Context.Abort();
                return;
            }
            var connection = new UserConnection
            {
                ConnectionId = Context.ConnectionId,
                UserId = userId,
                Ip = ip!,
                Hub = "/api/chathub",
                ConnectedAt = DateTime.UtcNow
            };
            _db.UserConnections.Add(connection);
            await _db.SaveChangesAsync();
            var userPosts = await _db.Posts
                .Where(p => p.Members.Any(m => m.Id == userId))
                .Select(p => p.Id)
                .ToListAsync();

            foreach (var postId in userPosts)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, postId);
            }
            Log.Information("User connected: UserId: {UserId}, IP: {IPAddress}, ConnectionId: {ConnectionId}", userId, ip, Context.ConnectionId);
            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred during OnConnectedAsync. ConnectionId: {ConnectionId}", Context.ConnectionId);
            Context.Abort();
        }
    }
    
    public async Task SendMessage(string postId, string content)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            Log.Warning("SendMessageToPostMembers aborted: Missing or empty user ID. ConnectionId: {ConnectionId}", Context.ConnectionId);
            return;
        }
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                Log.Warning("SendMessageToPostMembers aborted: User not found. UserId: {UserId}, ConnectionId: {ConnectionId}", userId, Context.ConnectionId);
                return;
            }
            var post = await _db.Posts
                .Include(p => p.Members)
                .FirstOrDefaultAsync(p => p.Id == postId);
            if (post == null)
            {
                Log.Warning("SendMessageToPostMembers aborted: Post not found. PostId: {PostId}, UserId: {UserId}", postId, userId);
                return;
            }

            var message = new Message()
            {
                Content = content,
                SenderId = user.Id,
                PostId = post.Id
            };
            _db.Messages.Add(message);
            await _db.SaveChangesAsync();
            var excludedConnectionIds = await _db.UserConnections
                .Where(uc => uc.UserId == user.Id && uc.Hub == "/api/chathub")
                .Select(uc => uc.ConnectionId)
                .ToListAsync();
            await Clients.GroupExcept(postId, excludedConnectionIds).SendAsync("ReceiveMessage", postId, message);
            Log.Information("Message sent to post members. PostId: {PostId}, UserId: {UserId}, ExcludedConnections: {ExcludedConnections}, Message: {Message}", 
                postId, userId, excludedConnectionIds, message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred while sending message to post members. ConnectionId: {ConnectionId}", Context.ConnectionId);
        }
    }
    public async Task UpdateMessage(string postId, string messageId, string newContent)
    { 
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value; 
        if (string.IsNullOrEmpty(userId)) 
        { 
            Log.Warning("UpdateMessage aborted: Missing or empty user ID. ConnectionId: {ConnectionId}", Context.ConnectionId); 
            return; 
        }
        
        try 
        { 
            var user = await _userManager.FindByIdAsync(userId); 
            if (user == null) 
            { 
                Log.Warning("UpdateMessage aborted: User not found. UserId: {UserId}, ConnectionId: {ConnectionId}", userId, Context.ConnectionId); 
                return; 
            } 
            var message = await _db.Messages
                .Include(m => m.Sender)
                .FirstOrDefaultAsync(m => m.Id == messageId && m.PostId == postId);
            
            if (message == null) 
            { 
                Log.Warning("UpdateMessage aborted: Message not found. MessageId: {MessageId}, PostId: {PostId}", messageId, postId); 
                return; 
            }
            
            if (message.SenderId != user.Id) 
            { 
                Log.Warning("UpdateMessage aborted: Unauthorized attempt to update message. UserId: {UserId}, MessageId: {MessageId}, PostId: {PostId}", user.Id, messageId, postId); 
                return; 
            } 
            message.Content = newContent; 
            message.UpdatedAt = DateTime.UtcNow; 
            await _db.SaveChangesAsync(); 
            var responseDto = _mapper.Map<MessageResponseDto>(message);
            var excludedConnectionIds = await _db.UserConnections
                .Where(uc => uc.UserId == user.Id && uc.Hub == "/api/chathub")
                .Select(uc => uc.ConnectionId)
                .ToListAsync(); 
            await Clients.GroupExcept(postId, excludedConnectionIds).SendAsync("UpdateMessage", postId, messageId, responseDto); 
            Log.Information("Message updated. PostId: {PostId}, MessageId: {MessageId}, UserId: {UserId}", postId, messageId, user.Id); 
        }
        catch (Exception ex) 
        { 
            Log.Error(ex, "Error occurred while updating message. ConnectionId: {ConnectionId}", Context.ConnectionId); 
        } 
    }
    public async Task DeleteMessage(string postId, string messageId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            Log.Warning("DeleteMessage aborted: Missing or empty user ID. ConnectionId: {ConnectionId}", Context.ConnectionId);
            return;
        }

        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                Log.Warning("DeleteMessage aborted: User not found. UserId: {UserId}, ConnectionId: {ConnectionId}", userId, Context.ConnectionId);
                return;
            }

            var message = await _db.Messages.FirstOrDefaultAsync(m => m.Id == messageId && m.PostId == postId);
            if (message == null)
            {
                Log.Warning("DeleteMessage aborted: Message not found. MessageId: {MessageId}, PostId: {PostId}", messageId, postId);
                return;
            }

            if (message.SenderId != user.Id)
            {
                Log.Warning("DeleteMessage aborted: Unauthorized attempt to delete message. UserId: {UserId}, MessageId: {MessageId}, PostId: {PostId}", user.Id, messageId, postId);
                return;
            }

            _db.Messages.Remove(message);
            await _db.SaveChangesAsync();

            var excludedConnectionIds = await _db.UserConnections
                .Where(uc => uc.UserId == user.Id && uc.Hub == "/api/chathub")
                .Select(uc => uc.ConnectionId)
                .ToListAsync();

            await Clients.GroupExcept(postId, excludedConnectionIds).SendAsync("DeleteMessage", postId, messageId);
            Log.Information("Message deleted. PostId: {PostId}, MessageId: {MessageId}, UserId: {UserId}", postId, messageId, user.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred while deleting message. ConnectionId: {ConnectionId}", Context.ConnectionId);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var connectionId = Context.ConnectionId;
            var userId = Context?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(connectionId, userId);
            }
            var userConnection = await _db.UserConnections.FirstOrDefaultAsync(uc => uc.ConnectionId == connectionId);
            if (userConnection != null)
            {
                _db.UserConnections.Remove(userConnection);
                await _db.SaveChangesAsync();
                Log.Information("User disconnected: UserId: {UserId}, ConnectionId: {ConnectionId}", userConnection.UserId, connectionId);
            }
            else
            {
                Log.Warning("Connection not found in the database during disconnection. ConnectionId: {ConnectionId}", connectionId);
            }
            if (exception != null)
            {
                Log.Error(exception, "An error occurred during disconnection. ConnectionId: {ConnectionId}", connectionId);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error occurred while processing OnDisconnectedAsync. ConnectionId: {ConnectionId}", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}