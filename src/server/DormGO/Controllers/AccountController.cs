using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using DormGO.Data;
using DormGO.DTOs;
using DormGO.Hubs;
using DormGO.Models;
using DormGO.Services;
using MapsterMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace DormGO.Controllers;

[ApiController]
[Route("api")]
public class AccountController : ControllerBase
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
        _emailSender = emailSender;
        _mapper = mapper;
    }

    [HttpPost("signup")]
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

    [HttpPost("signin")]
    public async Task<IActionResult> Login([FromBody] UserDto dto)
    {
        var user = await _db.Users.Include(u => u.RefreshSessions).FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user == null)
        {
            return Unauthorized("Invalid email");
        }

        if (!user.EmailConfirmed)
        {
            return BadRequest("Email is not confirmed. Please check your email for the confirmation link.");
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, dto.Password);

        if (!isPasswordValid)
        {
            return Unauthorized("Invalid password.");
        }

        if (user.RefreshSessions.Count >= 5)
        {
            _db.RefreshSessions.RemoveRange(user.RefreshSessions);
            await _db.SaveChangesAsync();
        }

        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        var session = new RefreshSession
        {
            UserId = user.Id,
            RefreshToken = refreshToken,
            Fingerprint = dto.VisitorId,
            UA = Request.Headers["User-Agent"].ToString(),
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ExpiresIn = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds()
        };

        _db.RefreshSessions.Add(session);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            Message = "Login successful",
            access_token = accessToken,
            refresh_token = refreshToken
        });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] EmailDto emailDto)
    {
        var user = await _userManager.FindByEmailAsync(emailDto.Email);
        if (user == null)
        {
            return NotFound("User not found.");
        }
        await SendForgotPasswordEmailAsync(user);
        return Ok("Forgot password email sent successfully.");
    }
    [HttpGet("reset-password")]
    public async Task<IActionResult> ResetPassword(string userId, string token)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            return BadRequest("The link is invalid or expired.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound("The user is not found.");
        }
        var isTokenValid = await _userManager.VerifyUserTokenAsync(
            user, 
            _userManager.Options.Tokens.PasswordResetTokenProvider, 
            "ResetPassword", 
            token
        );
        if (!isTokenValid)
        {
            return BadRequest("The link is invalid or expired.");
        }
        return Ok(new
        {
            Message = "The link is valid. You can now reset your password.",
            Email = user.Email!,
            Token = token
        });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto model)
    {
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            return NotFound("The user is not found.");
        }
        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }
        return Ok(new { Message = "Your password has been reset successfully." });
    }
    [HttpPost("signout")]
    public async Task<IActionResult> Logout([FromBody] TokenDto dto)
    {
        var session = await _db.RefreshSessions.FirstOrDefaultAsync(x => x.RefreshToken == dto.RefreshToken);

        if (session == null)
        {
            return BadRequest("The refresh token is not found");
        }

        _db.RefreshSessions.Remove(session);
        await _db.SaveChangesAsync();

        return Ok("The refresh token was successfully removed");
    }
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(string userId, string token, string visitorId,
        [FromServices] IHubContext<UserHub> hub)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            return BadRequest("The link is invalid or expired.");
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

        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        var session = new RefreshSession
        {
            UserId = user.Id,
            RefreshToken = refreshToken,
            Fingerprint = visitorId,
            UA = Request.Headers["User-Agent"].ToString(),
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ExpiresIn = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds()
        };

        _db.RefreshSessions.Add(session);
        await _db.SaveChangesAsync();

        var dto = new TokenDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };

        var connections = await _db.UserConnections
            .Where(c => c.UserId == userId)
            .Select(c => c.ConnectionId)
            .ToListAsync();

        foreach (var connectionId in connections)
        {
            await hub.Clients.Client(connectionId).SendAsync("EmailConfirmed", user.UserName, dto);
        }

        return Ok(new { Message = "Email confirmed successfully" });
    }

    [HttpPost("resend-confirmation-email")]
    public async Task<IActionResult> ResendConfirmationEmail([FromBody] EmailDto emailDto)
    {
        var user = await _userManager.FindByEmailAsync(emailDto.Email);
        if (user == null)
        {
            return NotFound("User not found.");
        }
        if (await _userManager.IsEmailConfirmedAsync(user))
        {
            return BadRequest("Email is already confirmed.");
        }
        user.Fingerprint = emailDto.VisitorId;
        await SendConfirmationEmailAsync(user);
        return Ok(new { Message = "Confirmation email sent successfully." });
    }

    [HttpPost("refresh-tokens")]
    public async Task<IActionResult> RefreshTokens([FromBody] RefreshTokenDto dto)
    {
        if (string.IsNullOrEmpty(dto.AccessToken) || string.IsNullOrEmpty(dto.RefreshToken))
        {
            return BadRequest(new { Message = "Access token and refresh token are required." });
        }

        var principal = GetPrincipalFromExpiredToken(dto.AccessToken);

        if (principal == null)
        {
            return Unauthorized(new { Message = "Invalid access token." });
        }

        var userEmail = principal.Identity?.Name;

        if (string.IsNullOrEmpty(userEmail))
        {
            return Unauthorized(new { Message = "Invalid access token." });
        }

        var session = await _db.RefreshSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.RefreshToken == dto.RefreshToken && s.User.Email == userEmail);

        if (session == null || session.ExpiresIn < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            return Unauthorized(new { Message = "Invalid or expired refresh token." });
        }

        if (dto.VisitorId != session.Fingerprint)
        {
            return BadRequest("Forged visitor ID.");
        }

        var user = await _userManager.FindByEmailAsync(userEmail);

        if (user == null)
        {
            return Unauthorized(new { Message = "User is not found." });
        }

        var newAccessToken = GenerateAccessToken(user);
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
        var confirmationLink = Url.Action(
            "ConfirmEmail",
            "Account",
            new { userId = user.Id, token = token, visitorId = user.Fingerprint },
            protocol: HttpContext.Request.Scheme
        );

        var body = $"Please confirm your email by <a href='{HtmlEncoder.Default.Encode(confirmationLink)}'>clicking here</a>.";

        await _emailSender.SendEmailAsync(user.Email!, "Confirm your email", body, true);
    }

    [NonAction]
    private async Task SendForgotPasswordEmailAsync(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user, nameof(user));
        string token = await _userManager.GeneratePasswordResetTokenAsync(user);
        string resetLink = Url.Action(
            "ResetPassword",
            "Account",
            new { userId = user.Id, token = token },
            protocol: HttpContext.Request.Scheme);
        string emailSubject = "Reset Your Password";
        string emailBody = $@"
        <p>Hi {user.UserName},</p>
        <p>You requested to reset your password. Click the link below to reset it:</p>
        <p><a href='{resetLink}'>Reset Password</a></p>
        <p>If you did not request this, please ignore this email.</p>";
        await _emailSender.SendEmailAsync(user.Email!, emailSubject, emailBody, true);
    }
    [NonAction]
    private string GenerateAccessToken(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.Email, user.Email),
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
            ValidateLifetime = false
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