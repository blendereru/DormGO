using System.ComponentModel;
using DormGO.Constants;
using DormGO.Data;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Filters;
using DormGO.Models;
using DormGO.Services;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DormGO.Controllers;
[Authorize]
[ApiController]
[ServiceFilter<ValidateUserEmailFilter>]
[Route("/api/notifications")]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public class NotificationController : ControllerBase
{
    private readonly ApplicationContext _db;
    private readonly ILogger<NotificationController> _logger;
    private readonly IInputSanitizer _inputSanitizer;

    public NotificationController(ApplicationContext db, ILogger<NotificationController> logger,
        IInputSanitizer inputSanitizer)
    {
        _db = db;
        _logger = logger;
        _inputSanitizer = inputSanitizer;
    }
    [EndpointSummary("Retrieve notifications")]
    [EndpointDescription("Retrieve all notifications of current user")]
    [ProducesResponseType<List<NotificationResponse>>(StatusCodes.Status200OK, "application/json")]
    [HttpGet]
    public async Task<ActionResult> GetAllNotifications()
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("Notifications read attempted with missing or invalid user context.");
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User information is missing.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }

        var postNotifications = await _db.PostNotifications
            .Where(pn => pn.UserId == user.Id)
            .Include(pn => pn.Post)
            .ThenInclude(p => p.Creator)
            .ProjectToType<PostNotificationResponse>()
            .ToListAsync();
        _logger.LogInformation("Notifications retrieved successfully. UserId: {UserId}, NotificationsCount: {NotificationCount}", user.Id, postNotifications.Count);
        return Ok(postNotifications);
    }
    [EndpointSummary("Update notification")]
    [EndpointDescription("Update the notification of current user")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateNotification([Description("Id of the notification to update")] string id, NotificationUpdateRequest updateRequest)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("Notification marking attempted with missing or invalid user context.");
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User information is missing.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }

        var anyChange = false;
        if(string.IsNullOrWhiteSpace(id)) 
        {
            _logger.LogWarning("Id not provided during notification update. UserId: {UserId}", user.Id);
            ModelState.AddModelError(nameof(id), "The id field is required.");
            return ValidationProblem(ModelState);
        }
        var sanitizedNotificationId = _inputSanitizer.Sanitize(id);
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == sanitizedNotificationId && n.UserId == user.Id);
        if (notification == null)
        {
            _logger.LogWarning("Notification update requested for non-existent notification. UserId: {UserId}, NotificationId: {NotificationId}", user.Id, sanitizedNotificationId);
            return NotFound(new ProblemDetails
            {
                Title = "Not found",
                Detail = "Notification not found.",
                Status = StatusCodes.Status404NotFound,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        if (updateRequest.IsRead.HasValue && notification.IsRead != updateRequest.IsRead)
        {
            anyChange = true;
            _logger.LogInformation("Marking the notification as read. UserId: {UserId}, NotificationId: {NotificationId}", user.Id, notification.Id);
            notification.IsRead = updateRequest.IsRead.Value;
        }

        if (!anyChange)
        {
            _logger.LogWarning("Notification update performed with no valid fields for UserId: {UserId}. NotificationId: {NotificationId}", user.Id, notification.Id);
            return BadRequest(new ProblemDetails
            {
                Title = "No Update Performed",
                Detail = "No valid fields were provided for update.",
                Status = StatusCodes.Status400BadRequest,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        await _db.SaveChangesAsync();
        _logger.LogInformation("Notification {NotificationId} updated for user {UserId}.", notification.Id, user.Id);
        return NoContent();
    }
    [EndpointSummary("Delete the notification")]
    [EndpointDescription("Delete the current user's notification")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNotification([Description("Id of the notification to delete")] string id)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("Notification deletion attempted with missing or invalid user context.");
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User information is missing.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        if(string.IsNullOrWhiteSpace(id)) 
        {
            _logger.LogWarning("Id not provided during notification delete. UserId: {UserId}", user.Id);
            ModelState.AddModelError(nameof(id), "The id field is required.");
            return ValidationProblem(ModelState);
        }
        var sanitizedNotificationId = _inputSanitizer.Sanitize(id);
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == sanitizedNotificationId && n.UserId == user.Id);
        if (notification == null)
        {
            _logger.LogWarning("Notification deletion requested for non-existent notification. UserId: {UserId}, NotificationId: {NotificationId}", user.Id, sanitizedNotificationId);
            return NotFound(new ProblemDetails
            {
                Title = "Not found",
                Detail = "Notification not found.",
                Status = StatusCodes.Status404NotFound,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        _db.Notifications.Remove(notification);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Notification {NotificationId} deleted. UserId: {UserId}", notification.Id, user.Id);
        return NoContent();
    }
}
