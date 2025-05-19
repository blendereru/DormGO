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

    [HttpPut("me/username")]
    public async Task<IActionResult> UpdateUsername([FromBody] UsernameUpdateRequestDto updateRequest)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("Username update attempted with missing or invalid user context.");
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User information is missing.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        var result = await _userManager.SetUserNameAsync(user, updateRequest.UserName);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }
            _logger.LogWarning("Username update failed. UserId: {UserId}, Errors: {Errors}", user.Id, result.Errors.Select(e => e.Description));
            return ValidationProblem(ModelState);
        }
        _logger.LogInformation("Username updated successfully. UserId: {UserId}", user.Id);
        return NoContent();
    }
    [HttpPut("me/email")]
    public async Task<IActionResult> UpdateEmail([FromBody] EmailUpdateRequestDto updateRequest)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("Email update attempted with missing or invalid user context.");
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User information is missing.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }
        if (_emailSender is not EmailSender emailSender)
        {
            _logger.LogWarning("EmailSender type doesn't match.");
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
        return Ok(new { Message = "Please confirm the link sent to new email" });
    }

    [HttpPost("password/check")]
    public async Task<IActionResult> CheckCurrentPassword([FromBody] CurrentPasswordCheckRequestDto passwordCheckRequestDto)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            _logger.LogWarning("Password check attempted with missing or invalid user context.");
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User information is missing.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = $"{Request.Method} {Request.Path}"
            });
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, passwordCheckRequestDto.CurrentPassword);
        if (!isPasswordValid)
        {
            _logger.LogWarning("Password is incorrect for UserId {UserId}", user.Id);
            ModelState.AddModelError(nameof(passwordCheckRequestDto.CurrentPassword), "The password is incorrect");
            return ValidationProblem(ModelState);
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetLink = Url.Action("ResetPassword", "Account", new
        {
            userId = user.Id,
            token
        }, Request.Scheme);
        await _emailSender.SendPasswordResetLinkAsync(user, user.Email!, resetLink!);
        return NoContent();
    }
    [HttpGet("{email}")]
    public async Task<IActionResult> GetUserProfile([FromRoute] string email)
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