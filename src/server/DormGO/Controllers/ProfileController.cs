using DormGO.Constants;
using DormGO.Data;
using DormGO.DTOs.RequestDTO;
using DormGO.Filters;
using DormGO.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace DormGO.Controllers;
[Authorize]
[ApiController]
[ServiceFilter<ValidateUserEmailFilter>]
[Route("api/profile")]
public class ProfileController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationContext _db;
    public ProfileController(UserManager<ApplicationUser> userManager, ApplicationContext db)
    {
        _userManager = userManager;
        _db = db;
    }
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        Log.Information("GetMyProfile: Profile read for user: {Email}", user.Email);
        return Ok(new
        {
            Email = user.Email,
            Name = user.UserName,
            RegisteredAt = user.RegistrationDate
        });
    }

    [HttpPut]
    public async Task<IActionResult> UpdateUsername([FromBody] UsernameUpdateRequestDto updateRequest)
    {
        if (!HttpContext.Items.TryGetValue(HttpContextItemKeys.UserItemKey, out var userObj) || userObj is not ApplicationUser user)
        {
            return Unauthorized(new { Message = "User information is missing." });
        }
        var result = await _userManager.SetUserNameAsync(user, updateRequest.UserName);
        if (!result.Succeeded)
        {
            Log.Warning("UpdateUsername: Failed to update username. UserId: {UserId}. Errors: {Errors}",
                user.Id, result.Errors.Select(e => e.Description));
            return BadRequest(result.Errors);
        }
        Log.Information("Update username: Username updated successfully. UserId: {UserId}", user.Id);
        return Ok(new { Message = "Username updated successfully" });
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
            Log.Warning("GetUserProfile: The email is missing");
            return BadRequest(new { Message = "Email is required." });
        }
        var userToSearch = await _userManager.FindByEmailAsync(email);
        if (userToSearch == null)
        {
            Log.Warning("GetUserProfile: The user with email {Email} is not found", email);
            return NotFound(new { Message = "User not found." });
        }
        Log.Information("GetUserProfile: Successfully retrieved user info. User id {UserId}", userToSearch.Id);
        return Ok(new 
        {
            Email = userToSearch.Email,
            Name = userToSearch.UserName,
            RegisteredAt = userToSearch.RegistrationDate
        });
    }
}