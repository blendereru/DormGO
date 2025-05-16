using System.Security.Claims;
using DormGO.Constants;
using DormGO.Data;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Filters;
using DormGO.Models;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DormGO.Controllers;
[Authorize]
[ApiController]
[ServiceFilter<ValidateUserEmailFilter>]
[Route("/api/notifications/")]
public class NotificationController : ControllerBase
{
    private readonly ApplicationContext _db;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(ApplicationContext db, ILogger<NotificationController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationRequestDto>>> GetAllNotifications()
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        var notifications = await _db.Notifications
            .Where(n => n.UserId == user.Id)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        if (notifications.Count <= 0)
        {
            _logger.LogInformation("No notifications found for user {UserId}", user.Id);
            return Ok(new List<NotificationResponseDto>());
        }
        var notificationDtos = notifications.Adapt<List<NotificationResponseDto>>();
        _logger.LogInformation("Notification retrieved successfully. UserId: {UserId}, NotificationsCount: {NotificationCount}", user.Id, notificationDtos.Count);
        return Ok(notificationDtos);
    }

    [HttpPut("{id}/mark-as-read")]
    public async Task<IActionResult> MarkAsRead(string id)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id);

        if (notification == null)
        {
            _logger.LogWarning("Notification marking requested for non-existent notification. UserId: {UserId}, NotificationId: {NotificationId}", user.Id, id);
            return NotFound(new { Message = "Notification not found." });
        }
        notification.IsRead = true;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Notification {NotificationId} marked as read for user {UserId}.", id, user.Id);
        return Ok(new { Message = "The notification was marked as read." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNotification(string id)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id);
        if (notification == null)
        {
            _logger.LogWarning("Notification remove requested for non-existent notification. UserId: {UserId}, NotificationId: {NotificationId}", user.Id, id);
            return NotFound(new { Message = "Notification not found." });
        }
        _db.Notifications.Remove(notification);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Notification {NotificationId} removed. UserId: {UserId}", id, user.Id);
        return Ok(new { Message = "The notification was successfully deleted" });
    }
}
