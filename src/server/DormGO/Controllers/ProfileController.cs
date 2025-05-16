using DormGO.Constants;
using DormGO.DTOs.RequestDTO;
using DormGO.Filters;
using DormGO.Models;
using DormGO.Services;
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
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
        }
    }
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        _logger.LogInformation("Profile read successfully. UserId: {UserId}", user.Id);
        return Ok(new
        {
            Email = user.Email,
            Name = user.UserName,
            RegisteredAt = user.RegistrationDate
        });
    }

    [HttpPut("username")]
    public async Task<IActionResult> UpdateUsername([FromBody] UsernameUpdateRequestDto updateRequest)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        var result = await _userManager.SetUserNameAsync(user, updateRequest.UserName);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            _logger.LogWarning("Username update failed. UserId: {UserId}, Errors: {Errors}", user.Id, errors);
            return BadRequest(errors);
        }
        _logger.LogInformation("Username updated successfully. UserId: {UserId}", user.Id);
        return Ok(new { Message = "Username updated successfully" });
    }
    [HttpPut("email")]
    public async Task<IActionResult> UpdateEmail([FromBody] EmailUpdateRequestDto updateRequest)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        if (_emailSender is not EmailSender emailSender)
        {
            _logger.LogWarning("EmailSender type doesn't match.");
            return StatusCode(500, new { Message = "Email sending service is not available" });
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
            return Unauthorized(new { Message = "User information is missing." });
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, passwordCheckRequestDto.CurrentPassword);
        if (!isPasswordValid)
        {
            _logger.LogWarning("Password is incorrect for UserId {UserId}", user.Id);
            return BadRequest(new { Message = "Password is incorrect" });
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetLink = Url.Action("ResetPassword", "Account", new
        {
            userId = user.Id,
            token
        }, Request.Scheme);
        await _emailSender.SendPasswordResetLinkAsync(user, user.Email!, resetLink!);
        return Ok(new { Message = "Password verified successfully" });
    }
    [HttpGet("{email}")]
    public async Task<IActionResult> GetUserProfile(string email)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        if (string.IsNullOrEmpty(email))
        {
            _logger.LogWarning("Email of user to search is not provided during profile search. UserId: {UserId}", user.Id);
            return BadRequest(new { Message = "Email is required." });
        }
        var userToSearch = await _userManager.FindByEmailAsync(email);
        if (userToSearch == null)
        {
            _logger.LogInformation("User with specified email not found during profile search. UserId: {UserId}", user.Id);
            return NotFound(new { Message = "User not found." });
        }
        _logger.LogInformation("User profile search completed successfully. UserToSearchId: {UserToSearchId}, UserId: {UserId}", userToSearch.Id, user.Id);
        return Ok(new 
        {
            Email = userToSearch.Email,
            Name = userToSearch.UserName,
            RegisteredAt = userToSearch.RegistrationDate
        });
    }
}