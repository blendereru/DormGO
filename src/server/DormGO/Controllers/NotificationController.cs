using System.Security.Claims;
using DormGO.Data;
using DormGO.DTOs;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DormGO.Controllers;
[Authorize]
[ApiController]
[Route("/api/notifications/")]
public class NotificationController : ControllerBase
{
    private readonly ApplicationContext _db;

    public NotificationController(ApplicationContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationRequestDto>>> GetAllNotifications()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            Log.Warning("GetAllNotifications: Unauthorized access attempt.");
            return Unauthorized(new { Message = "User is not authenticated." });
        }

        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        if (notifications.Count <= 0)
        {
            Log.Information("GetAllNotifications: No notifications found for user {UserId}.", userId);
            return Ok(new List<NotificationResponseDto>());
        }

        var notificationDtos = notifications.Adapt<List<NotificationResponseDto>>();
        Log.Information("GetAllNotifications: Retrieved {Count} notifications for user {UserId}.", notificationDtos.Count, userId);
        return Ok(notificationDtos);
    }

    [HttpPut("{id}/mark-as-read")]
    public async Task<IActionResult> MarkAsRead(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            Log.Warning("MarkAsRead: Unauthorized access attempt.");
            return Unauthorized(new { Message = "User is not authenticated." });
        }

        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

        if (notification == null)
        {
            Log.Warning("MarkAsRead: Notification {NotificationId} not found for user {UserId}.", id, userId);
            return NotFound(new { Message = "Notification not found." });
        }
        notification.IsRead = true;
        await _db.SaveChangesAsync();
        Log.Information("MarkAsRead: Notification {NotificationId} marked as read for user {UserId}.", id, userId);
        return Ok(new { Message = "The notification was marked as read." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNotification(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            Log.Warning("DeleteNotification: Unauthorized access attempt.");
            return Unauthorized(new { Message = "User is not authenticated." });
        }
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (notification == null)
        {
            Log.Warning("DeleteNotification: Notification {NotificationId} not found for user {UserId}.", id, userId);
            return NotFound(new { Message = "Notification not found." });
        }
        _db.Notifications.Remove(notification);
        await _db.SaveChangesAsync();
        Log.Information("DeleteNotification: Notification {NotificationId} deleted for user {UserId}.", id, userId);
        return Ok(new { Message = "The notification was successfully deleted" });
    }
}
