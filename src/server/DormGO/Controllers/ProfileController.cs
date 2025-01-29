using System.Security.Claims;
using DormGO.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace DormGO.Controllers;
[Authorize]
[ApiController]
[Route("api/profile")]
public class ProfileController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    public ProfileController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var emailClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(emailClaim))
        {
            Log.Warning("GetMyProfile: Email claim not found.");
            return Unauthorized("The email claim is not found.");
        }
        var user = await _userManager.FindByEmailAsync(emailClaim);
        if (user == null)
        {
            Log.Warning("GetMyProfile: User not found with email: {Email}", emailClaim);
            return NotFound("The user with the provided email is not found.");
        }
        Log.Information("GetMyProfile: Profile read for user: {Email}", emailClaim);
        return Ok(new
        {
            Email = emailClaim,
            Name = user.UserName,
            RegisteredAt = user.RegistrationDate
        });
    }

    [HttpGet("{email}")]
    public async Task<IActionResult> GetUserProfile(string email)
    {
        if (string.IsNullOrEmpty(email))
        {
            Log.Warning("GetUserProfile: The email is missing");
            return BadRequest("Email is required.");
        }
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            Log.Warning("GetUserProfile: The user with email {Email} is not found", email);
            return NotFound("User not found.");
        }
        Log.Information("GetUserProfile: Successfully retrieved user info. User id {UserId}", user.Id);
        return Ok(new 
        {
            email = user.Email,
            name = user.UserName,
            registeredAt = user.RegistrationDate
        });
    }
}