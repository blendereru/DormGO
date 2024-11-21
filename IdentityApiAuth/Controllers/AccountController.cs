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
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    public AccountController(UserManager<ApplicationUser> userManager, IEmailSender emailSender)
    {
        _userManager = userManager;
        _emailSender  = emailSender;
    }
    [HttpPost("/api/signup")]
    public async Task<IActionResult> Register([FromBody] UserModel model)
    {
        var user = new ApplicationUser()
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
        return Ok(new { Message = "User registered successfully. Email confirmation is pending." });
    }
    [HttpGet("/api/check-confirmation/{email}")]
    public async Task<IActionResult> CheckEmailConfirmationStatus(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return NotFound("User not found");
        }
        if (await _userManager.IsEmailConfirmedAsync(user))
        {
            var jwtToken = GenerateJwtToken(user.Email!);
            return Ok(new { Message = "Email confirmed successfully.", Token = jwtToken });
        }
        return Ok(new { Message = "Email not confirmed yet." });
    }
    [HttpPost("/api/signin")]
    public async Task<IActionResult> Login([FromBody]UserModel model)
    {
        var user = await _userManager.FindByEmailAsync(model.Email!);
        if (user == null)
        {
            return Unauthorized("Invalid email or password.");
        }
        if (!user.EmailConfirmed)
        {
            return BadRequest("Email is not confirmed. Please check your email for the confirmation link.");
        }
        var isPasswordValid = await _userManager.CheckPasswordAsync(user, model.Password);
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

        return Ok(new { Message = "Email confirmed successfully" });
    }
    [NonAction]
    private async Task SendConfirmationEmailAsync(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user, nameof(user));
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationLink = $"https://8440-188-127-36-2.ngrok-free.app{Url.Action("ConfirmEmail", "Account", new { userId = user.Id, token = token })}";
        var body = $"Please confirm your email by <a href='{HtmlEncoder.Default.Encode(confirmationLink)}'>clicking here</a>.";
        await _emailSender.SendEmailAsync(user.Email!, "Confirm your email", body, true);
    }
    [NonAction]
    private string GenerateJwtToken(string email)
    {
        var claims = new List<Claim>()
        {
            new Claim(ClaimTypes.Name, email),
            new Claim(ClaimTypes.Email, email),
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