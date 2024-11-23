using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using IdentityApiAuth.DTOs;
using IdentityApiAuth.Models;
using MapsterMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace IdentityApiAuth.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationContext _db;
    private readonly IEmailSender _emailSender;
    private readonly IMapper _mapper;
    public AccountController(UserManager<ApplicationUser> userManager, ApplicationContext db,
        IEmailSender emailSender, IMapper mapper)
    {
        _userManager = userManager;
        _db = db;
        _emailSender  = emailSender;
        _mapper = mapper;
    }
    [HttpPost("/api/signup")]
    public async Task<IActionResult> Register([FromBody] UserDto dto)
    {
        var user = _mapper.Map<ApplicationUser>(dto);
        user.RegistrationDate = DateTime.UtcNow;
        var result = await _userManager.CreateAsync(user, dto.Password);
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
            var existingSession = _db.RefreshSessions
                .FirstOrDefault(session => session.UserId == user.Id);
            if (existingSession != null)
            {
                return Ok(new 
                { 
                    Message = "Email already confirmed.", 
                    Token = "Token already issued. Use the previously provided token." 
                });
            }
            var jwtToken = GenerateAccessToken(user.Email!);
            var refreshToken = GenerateRefreshToken();
            var session = new RefreshSession()
            {
                UserId = user.Id,
                RefreshToken = refreshToken,
                UA = Request.Headers["User-Agent"].ToString(),
                Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
                ExpiresIn = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds()
            };
            _db.RefreshSessions.Add(session);
            await _db.SaveChangesAsync();
            return Ok(new
            {
                Message = "Email confirmed successfully.",
                access_token = jwtToken,
                refresh_token = refreshToken
            });
        }
        return Ok(new { Message = "Email not confirmed yet." });
    }

    [HttpPost("/api/signin")]
    public async Task<IActionResult> Login([FromBody]UserDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email!);
        if (user == null)
        {
            return Unauthorized("Invalid email or password.");
        }
        if (!user.EmailConfirmed)
        {
            return BadRequest("Email is not confirmed. Please check your email for the confirmation link.");
        }
        var isPasswordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
        if (!isPasswordValid)
        {
            return Unauthorized("Invalid email or password.");
        }
        var token = GenerateAccessToken(user.Email!);
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
    [HttpPost("/api/refresh-tokens")]
    public async Task<IActionResult> RefreshTokens([FromBody] TokenRequest request)
    {
        // Step 1: Validate the input
        if (string.IsNullOrEmpty(request.AccessToken) || string.IsNullOrEmpty(request.RefreshToken))
        {
            return BadRequest(new { Message = "Invalid request. Access token and refresh token are required." });
        }
        var principal = GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal == null)
        {
            return Unauthorized(new { Message = "Invalid access token." });
        }
        var userEmail = principal.Identity?.Name;
        if (string.IsNullOrEmpty(userEmail))
        {
            return Unauthorized(new { Message = "Invalid access token." });
        }
        var session = _db.RefreshSessions
            .FirstOrDefault(s => s.RefreshToken == request.RefreshToken && s.User.Email == userEmail);
        if (session == null || session.ExpiresIn < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            return Unauthorized(new { Message = "Invalid or expired refresh token." });
        }
        if (request.RefreshToken != session.RefreshToken)
        {
            return Unauthorized(new { Message = "Invalid refresh token." });
        }
        var newAccessToken = GenerateAccessToken(userEmail);
        var newRefreshToken = GenerateRefreshToken();
        session.RefreshToken = newRefreshToken;
        session.ExpiresIn = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds();
        session.UA = Request.Headers["User-Agent"].ToString();
        session.Ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        _db.RefreshSessions.Update(session);
        await _db.SaveChangesAsync();
        return Ok(new
        {
            access_token = newAccessToken,
            refresh_token = newRefreshToken
        });
    }
    [NonAction]
    private async Task SendConfirmationEmailAsync(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user, nameof(user));
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationLink = $"http://localhost:5093{Url.Action("ConfirmEmail", "Account", new { userId = user.Id, token = token })}";
        var body = $"Please confirm your email by <a href='{HtmlEncoder.Default.Encode(confirmationLink)}'>clicking here</a>.";
        await _emailSender.SendEmailAsync(user.Email!, "Confirm your email", body, true);
    }
    [NonAction]
    private string GenerateAccessToken(string email)
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
    [NonAction]
    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
    [NonAction]
    private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var key = AuthOptions.GetSymmetricSecurityKey();
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = false // Skip expiration validation
        };
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

            if (securityToken is JwtSecurityToken jwtToken &&
                jwtToken.Header.Alg == SecurityAlgorithms.HmacSha256)
            {
                return principal;
            }
        }
        catch
        {
        }
        return null;
    }
}