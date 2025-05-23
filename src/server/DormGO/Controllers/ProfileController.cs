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
public class ProfileController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender<ApplicationUser> _emailSender;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(UserManager<ApplicationUser> userManager, IEmailSender<ApplicationUser> emailSender,
        ILogger<ProfileController> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _logger = logger;
    }
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
        var responseDto = user.Adapt<ProfileResponseDto>();
        _logger.LogInformation("Profile read successfully. UserId: {UserId}", user.Id);
        return Ok(responseDto);
    }
    [HttpPatch("me")]
    public async Task<IActionResult> PatchMe(UserUpdateRequestDto updateRequest)
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
    [HttpGet("{email}")]
    public async Task<IActionResult> GetUserProfile(string email)
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
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Email of user to search is not provided during profile search. UserId: {UserId}", user.Id);
            ModelState.AddModelError(nameof(email), "Email is required.");
            return ValidationProblem(ModelState);
        }
        var userToSearch = await _userManager.FindByEmailAsync(email);
        if (userToSearch == null)
        {
            _logger.LogInformation("User with specified email not found during profile search. UserId: {UserId}", user.Id);
            return NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = "User not found.",
                Status = StatusCodes.Status404NotFound,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        var responseDto = userToSearch.Adapt<ProfileResponseDto>();
        _logger.LogInformation("User profile search completed successfully. UserToSearchId: {UserToSearchId}, UserId: {UserId}", userToSearch.Id, user.Id);
        return Ok(responseDto);
    }
}