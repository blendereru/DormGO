using System.ComponentModel;
using DormGO.Constants;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Filters;
using DormGO.Models;
using DormGO.Services;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DormGO.Controllers;

[Authorize]
[ApiController]
[ServiceFilter<ValidateUserEmailFilter>]
[Route("api/profile")]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public class ProfileController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender<ApplicationUser> _emailSender;
    private readonly IInputSanitizer _inputSanitizer;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(UserManager<ApplicationUser> userManager, IEmailSender<ApplicationUser> emailSender,
        IInputSanitizer inputSanitizer, ILogger<ProfileController> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _inputSanitizer = inputSanitizer;
        _logger = logger;
    }
    [EndpointSummary("Retrieve user's profile info")]
    [EndpointDescription("Retrieve current user's profile information")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK, "application/json")]
    [HttpGet("me")]
    public IActionResult GetMyProfile()
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("Profile read attempted with missing or invalid user context.");
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User information is missing.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        var response = user.Adapt<ProfileResponse>();
        _logger.LogInformation("Profile read successfully. UserId: {UserId}", user.Id);
        return Ok(response);
    }
    [EndpointSummary("Update user's profile")]
    [EndpointDescription("Update current user's profile information")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpPatch("me")]
    public async Task<IActionResult> UpdateMyProfile(UserUpdateRequest updateRequest)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("User update attempted with missing or invalid user context.");
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User information is missing.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        IdentityResult? result;
        var anyChange = false;
        if (!string.IsNullOrWhiteSpace(updateRequest.UserName))
        {
            result = await _userManager.SetUserNameAsync(user, updateRequest.UserName);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(error.Code, error.Description);
                }
                _logger.LogWarning("Username update failed for UserId: {UserId}, Errors: {Errors}", user.Id, result.Errors.Select(e => e.Description));
            }
            else
            {
                anyChange = true;
                _logger.LogInformation("Username updated for UserId: {UserId}", user.Id);
            }
        }
        if (!string.IsNullOrWhiteSpace(updateRequest.NewEmail))
        {
            if (_emailSender is not EmailSender emailSender)
            {
                _logger.LogError("Email sending service is not available for UserId: {UserId}", user.Id);
                return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Title = "Internal Server Error",
                    Detail = "Email sending service is not available.",
                    Status = StatusCodes.Status500InternalServerError,
                    Instance = $"{Request.Method} {Request.Path}"
                });
            }
            var token = await _userManager.GenerateChangeEmailTokenAsync(user, updateRequest.NewEmail);
            var changeLink = Url.Action("UpdateEmail", "Account", new
            {
                userId = user.Id,
                newEmail = updateRequest.NewEmail,
                token
            }, Request.Scheme);
            await emailSender.SendEmailChangeLinkAsync(user, updateRequest.NewEmail, changeLink!);
            anyChange = true;
            _logger.LogInformation("Email confirmation initiated for UserId: {UserId}", user.Id);
        }
        if (!string.IsNullOrWhiteSpace(updateRequest.NewPassword) || !string.IsNullOrWhiteSpace(updateRequest.ConfirmNewPassword))
        {
            var validationFailed = false;

            if (string.IsNullOrWhiteSpace(updateRequest.CurrentPassword))
            {
                ModelState.AddModelError(nameof(updateRequest.CurrentPassword), "Current password is required.");
                validationFailed = true;
                _logger.LogWarning("Password update failed: missing current password for UserId: {UserId}", user.Id);
            }
            if (string.IsNullOrWhiteSpace(updateRequest.NewPassword))
            {
                ModelState.AddModelError(nameof(updateRequest.NewPassword), "New password is required.");
                validationFailed = true;
                _logger.LogWarning("Password update failed: missing new password for UserId: {UserId}", user.Id);
            }
            if (string.IsNullOrWhiteSpace(updateRequest.ConfirmNewPassword))
            {
                ModelState.AddModelError(nameof(updateRequest.ConfirmNewPassword), "Please confirm your new password.");
                validationFailed = true;
                _logger.LogWarning("Password update failed: missing confirm new password for UserId: {UserId}", user.Id);
            }
            if (!validationFailed && updateRequest.NewPassword != updateRequest.ConfirmNewPassword)
            {
                ModelState.AddModelError(nameof(updateRequest.ConfirmNewPassword), "New passwords do not match.");
                validationFailed = true;
                _logger.LogWarning("Password update failed: new passwords do not match for UserId: {UserId}", user.Id);
            }

            if (!validationFailed && ModelState.ErrorCount == 0)
            {
                result = await _userManager.ChangePasswordAsync(user, updateRequest.CurrentPassword!, updateRequest.NewPassword!);
                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(error.Code, error.Description);
                    }
                    _logger.LogWarning("Password change failed for UserId: {UserId}, Errors: {Errors}", user.Id, result.Errors.Select(e => e.Description));
                }
                else
                {
                    anyChange = true;
                    _logger.LogInformation("Password changed for UserId: {UserId}", user.Id);
                }
            }
        }

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("User update validation failed for UserId: {UserId}. Errors: {Errors}", user.Id, string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return ValidationProblem(ModelState);
        }

        if (!anyChange)
        {
            _logger.LogWarning("User update performed with no valid fields for UserId: {UserId}", user.Id);
            return BadRequest(new ProblemDetails
            {
                Title = "No Update Performed",
                Detail = "No valid fields were provided for update.",
                Status = StatusCodes.Status400BadRequest,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        return NoContent();
    }
    [EndpointSummary("Retrieve a profile")]
    [EndpointDescription("Retrieve a specific user's profile")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK, "application/json")]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserProfile([Description("The id of the user to retrieve the profile of")] string id)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("User profile search attempted with missing or invalid user context.");
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User information is missing.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        if (string.IsNullOrWhiteSpace(id))
        {
            _logger.LogWarning("Id of user to search is not provided during profile search. InitiatorId: {InitiatorId}", user.Id);
            ModelState.AddModelError(nameof(id), "Email is required.");
            return ValidationProblem(ModelState);
        }

        var sanitizedUserId = _inputSanitizer.Sanitize(id);
        var userToSearch = await _userManager.FindByIdAsync(sanitizedUserId);
        if (userToSearch == null)
        {
            _logger.LogInformation("User with specified id not found during profile search. InitiatorId: {InitiatorId}, RequestedUserId: {RequestedUserid}", user.Id, sanitizedUserId);
            return NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = "User not found.",
                Status = StatusCodes.Status404NotFound,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        var response = userToSearch.Adapt<ProfileResponse>();
        _logger.LogInformation("User profile search completed successfully. UserToSearchId: {UserToSearchId}, UserId: {UserId}", userToSearch.Id, user.Id);
        return Ok(response);
    }
}