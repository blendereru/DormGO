using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Web;
using IdentityApiAuth.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace IdentityApiAuth.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IEmailSender _emailSender;
    public AccountController(UserManager<IdentityUser> userManager, IEmailSender emailSender)
    {
        _userManager = userManager;
        _emailSender  = emailSender;
    }
    [HttpPost("/api/signup")]
    public async Task<IActionResult> Register([FromBody]RequestModel model)
    {
        var user = new IdentityUser()
        {
            UserName = model.Email,
            Email = model.Email
        };
        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return BadRequest(ModelState);
        }
        await SendConfirmationEmailAsync(user);
        return Ok(new { Message = "User registered successfully", Status = "Email confirmation is required" });
    }
    
    [HttpPost("/api/signin")]
    public async Task<IActionResult> Login(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return Unauthorized("Invalid email or password.");
        }
        if (!user.EmailConfirmed)
        {
            return BadRequest("Email is not confirmed. Please check your email for the confirmation link.");
        }
        var isPasswordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!isPasswordValid)
        {
            return Unauthorized("Invalid email or password.");
        }
        var token = GenerateJwtToken(user.Email!);
        return Ok(new { Message = "Login successful", Token = token });
    }
    [HttpGet("/api/confirm-email")]
    public async Task<IActionResult> ConfirmEmail(string userId, string token)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            ViewBag.Message = "The link is Invalid or Expired";
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound("The user is not found");
        }
        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }
        var jwtToken = GenerateJwtToken(user.Email!);
        return Ok(new { Message = "Email confirmed successfully.", Token = jwtToken});
    }
    [NonAction]
    private async Task SendConfirmationEmailAsync(IdentityUser user)
    {
        ArgumentNullException.ThrowIfNull(user, nameof(user));
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationLink = $"https://0d44-2-134-108-133.ngrok-free.app{Url.Action("ConfirmEmail", "Account", new { userId = user.Id, token = token })}";
        var body = $"Please confirm your email by <a href='{HtmlEncoder.Default.Encode(confirmationLink)}'>clicking here</a>.";
        await _emailSender.SendEmailAsync(user.Email!, "Confirm your email", body, true);
    }
    [NonAction]
    private string GenerateJwtToken(string email)
    {
        var claims = new List<Claim>()
        {
            new Claim(ClaimTypes.Name, email),
            new Claim(ClaimTypes.NameIdentifier, email),
            new Claim(ClaimTypes.Role, "User")
        };
        var jwt = new JwtSecurityToken(
            issuer: AuthOptions.ISSUER,
            audience: AuthOptions.AUDIENCE,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(AuthOptions.LIFETIME),
            signingCredentials: new SigningCredentials(AuthOptions.GetSymmetricSecurityKey(), SecurityAlgorithms.HmacSha256)
        );
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}