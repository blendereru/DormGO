using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using DormGO.Constants;
using DormGO.Data;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Hubs;
using DormGO.Models;
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
    private readonly IEmailSender<ApplicationUser> _emailSender;
    private readonly ILogger<AccountController> _logger;
    private readonly IMapper _mapper;

    public AccountController(UserManager<ApplicationUser> userManager, ApplicationContext db,
        IEmailSender<ApplicationUser> emailSender, ILogger<AccountController> logger, IMapper mapper)
    {
        _userManager = userManager;
        _db = db;
        _emailSender = emailSender;
        _logger = logger;
        _mapper = mapper;
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Register([FromBody] UserRequestDto dto)
    {
        if (string.IsNullOrEmpty(dto.Password))
        {
            _logger.LogInformation("Password not provided during user registration. VisitorId: {VisitorId}", dto.VisitorId);
            return BadRequest(new { Message = "Invalid credentials"});
        }
        var user = _mapper.Map<ApplicationUser>(dto);
        user.RegistrationDate = DateTime.UtcNow;

        var result = await _userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            _logger.LogWarning("User registration failed. Errors: {Errors}", errors);
            return BadRequest(errors);
        }
        _logger.LogInformation("User registered successfully. UserId: {UserId}", user.Id);
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationLink = Url.Action("ConfirmEmail", "Account", new
        {
            userId = user.Id,
            token,
            visitorId = user.Fingerprint
        }, protocol: Request.Scheme);
        await _emailSender.SendConfirmationLinkAsync(user, user.Email!, confirmationLink!);
        return Ok(new { Message = "User registered successfully. Email confirmation is pending." });
    }

    [HttpPost("signin")]
    public async Task<IActionResult> Login([FromBody] UserRequestDto dto)
    {
        if (string.IsNullOrEmpty(dto.Password))
        {
            _logger.LogInformation("Password not provided during login. VisitorId: {VisitorId}", dto.VisitorId);
            return BadRequest(new { Message = "Invalid credentials" });
        }
        var user = await _db.Users.Include(u => u.RefreshSessions).FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null)
        {
            _logger.LogWarning("Login requested for non-existent user. VisitorId: {VisitorId}", dto.VisitorId);
            return Unauthorized(new { Message = "Invalid email" });
        }
        if (!user.EmailConfirmed)
        {
            _logger.LogWarning("User's email is not confirmed yet. Blocking user. UserId: {UserId}", user.Id);
            return BadRequest(new { Message = "Email is not confirmed. Please check your email for the confirmation link." });
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, dto.Password);

        if (!isPasswordValid)
        {
            _logger.LogWarning("Invalid password of user. UserId: {UserId}", user.Id);
            return Unauthorized(new { Message = "Invalid credentials." });
        }
        if (user.RefreshSessions.Count >= 5)
        {
            _db.RefreshSessions.RemoveRange(user.RefreshSessions);
            var count = user.RefreshSessions.Count;
            await _db.SaveChangesAsync();
            _logger.LogInformation("User's refresh sessions exceeded the limit and were cleared. UserId: {UserId}, ClearedSessionsCount: {Count}", user.Id, count);
        }

        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();
        var session = new RefreshSession
        {
            UserId = user.Id,
            RefreshToken = refreshToken,
            Fingerprint = dto.VisitorId,
            UA = Request.Headers.UserAgent.ToString(),
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ExpiresIn = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds()
        };

        _db.RefreshSessions.Add(session);
        await _db.SaveChangesAsync();
        _logger.LogInformation("User successfully logged in. UserId: {UserId}", user.Id);
        return Ok(new
        {
            Message = "Login successful",
            access_token = accessToken,
            refresh_token = refreshToken
        });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] UserRequestDto requestDto)
    {
        var user = await _userManager.FindByEmailAsync(requestDto.Email);
        if (user == null)
        {
            _logger.LogWarning("Password forgot requested for non-existent user. VisitorId: {VisitorId}", requestDto.VisitorId);
            return NotFound(new { Message = "Invalid credentials"});
        }
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetLink = Url.Action("ResetPassword", "Account", new
        {
            userId = user.Id,
            token
        }, protocol: Request.Scheme);
        await _emailSender.SendPasswordResetLinkAsync(user, user.Email!, resetLink!);
        return Ok(new { Message = "Forgot password email sent successfully."});
    }
    
    [HttpGet("update-email")]
    public async Task<IActionResult> UpdateEmail(string userId, string newEmail, string token, [FromServices] IHubContext<UserHub> hub)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Invalid or expired email change link. UserId: {UserId}", userId);
            return BadRequest(new { Message = "The link is invalid or expired."});
        }
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("Email change requested for non-existent user. UserId: {UserId}", userId);
            return NotFound(new { Message = "User is not found." });
        }
        var result = await _userManager.ChangeEmailAsync(user, newEmail, token);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            _logger.LogWarning("User email change failed. UserId: {UserId}. Errors: {Errors}", user.Id, errors);
            return BadRequest(errors);
        }
        _logger.LogInformation("Email changed successfully. UserId: {UserId}", user.Id);
        var connections = await _db.UserConnections
            .Where(c => c.UserId == userId)
            .Select(c => c.ConnectionId)
            .ToListAsync();
        if (connections.Count > 0)
        {
            await hub.Clients.Clients(connections).SendAsync("EmailChanged", new
            {
                Message = "Your email changed successfully.",
                Email = user.Email!,
                Token = token,
                Timestamp = DateTime.UtcNow
            });
            _logger.LogInformation("Message on email change sent to user on hub. UserId: {UserId}, ConnectionsCount: {Count}", user.Id, connections.Count);
        }
        else
        {
            _logger.LogWarning("No active connections found on hub. UserId: {UserId}", user.Id);
        }
        return Ok(new {Message = "Email changed successfully."});
    }
    [HttpGet("reset-password")]
    public async Task<IActionResult> ResetPassword(string userId, string token, [FromServices] IHubContext<UserHub> hub)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Invalid or expired password reset link. UserId: {UserId}", userId);
            return BadRequest(new { Message = "The link is invalid or expired."});
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User not found during password reset. UserId: {UserId}", userId);
            return NotFound(new { Message = "The user is not found." });
        }
        var connections = await _db.UserConnections
            .Where(c => c.UserId == userId)
            .Select(c => c.ConnectionId)
            .ToListAsync();
        if (connections.Count > 0)
        {
            await hub.Clients.Clients(connections).SendAsync("PasswordResetLinkValidated", new
            {
                Message = "Your password reset link has been verified.",
                Email = user.Email!,
                Token = token,
                Timestamp = DateTime.UtcNow
            });
            _logger.LogInformation("Message on password reset sent to user on hub. UserId: {UserId}, ConnectionsCount: {Count}", userId, connections.Count);
        }
        else
        {
            _logger.LogWarning("No active connections found on hub. UserId: {UserId}.", userId);
        }
        return Ok(new
        {
            Message = "The link is valid. You can now reset your password.",
        });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] PasswordResetRequest passwordResetRequest)
    {
        var user = await _userManager.FindByEmailAsync(passwordResetRequest.Email);
        if (user == null)
        {
            _logger.LogWarning("Password reset requested for non-existent user.");
            return NotFound(new { Message = "User is not found."});
        }

        var result = await _userManager.ResetPasswordAsync(user, passwordResetRequest.Token, passwordResetRequest.NewPassword);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            _logger.LogWarning("Password reset failed. UserId: {UserId}, Errors: {Errors}", user.Id, errors);
            return BadRequest(errors);
        }
        _logger.LogInformation("Password successfully reset. UserId: {UserId}", user.Id);
        return Ok(new { Message = "Your password has been reset successfully." });
    }
    [HttpDelete("signout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto dto)
    {
        var session = await _db.RefreshSessions.FirstOrDefaultAsync(x => x.RefreshToken == dto.RefreshToken);
        if (session == null)
        {
            _logger.LogWarning("Refresh session not found during logout. VisitorId: {VisitorId}", dto.VisitorId);
            return BadRequest(new { Message = "Invalid token"});
        }
        _db.RefreshSessions.Remove(session);
        await _db.SaveChangesAsync();
        _logger.LogInformation("User logged out successfully. VisitorId: {VisitorId}", dto.VisitorId);
        return Ok(new { Message = "User successfully logged out"});
    }
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(string userId, string token, string visitorId,
        [FromServices] IHubContext<UserHub> hub)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Invalid or expired link parameters. UserId: {UserId}", userId);
            return BadRequest(new { Message = "The link is invalid or expired."});
        }
        
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("Email confirmation requested for non-existent user. UserId: {UserId}", userId);
            return NotFound(new { Message = "The user is not found"});
        }
        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            _logger.LogWarning("Email confirmation failed. UserId: {UserId}, Errors: {Errors}", user.Id, errors);
            return BadRequest(errors);
        }
        _logger.LogInformation("Email successfully confirmed. UserId: {UserId}", user.Id);
        
        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();
        var session = new RefreshSession
        {
            UserId = user.Id,
            RefreshToken = refreshToken,
            Fingerprint = visitorId,
            UA = Request.Headers.UserAgent.ToString(),
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ExpiresIn = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds()
        };
        _db.RefreshSessions.Add(session);
        await _db.SaveChangesAsync();
        var dto = new RefreshTokenResponseDto()
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
            _logger.LogInformation("Message on email confirmation sent to user on hub. UserId: {UserId}", user.Id);
        }
        return Ok(new { Message = "Email confirmed successfully" });
    }

    [HttpPost("resend-confirmation-email")]
    public async Task<IActionResult> ResendConfirmationEmail([FromBody] UserRequestDto requestDto)
    {
        var user = await _userManager.FindByEmailAsync(requestDto.Email);
        if (user == null)
        {
            _logger.LogWarning("Email confirmation resend requested for non-existent user. VisitorId: {VisitorId}", requestDto.VisitorId);
            return NotFound(new { Message = "User not found."});
        }
        if (await _userManager.IsEmailConfirmedAsync(user))
        {
            _logger.LogInformation("Email already confirmed. Skipping the request. UserId: {UserId}", user.Id);
            return BadRequest(new { Message = "Email is already confirmed." });
        }
        user.Fingerprint = requestDto.VisitorId;
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationLink = Url.Action("ConfirmEmail", "Account", new
        {
            userId = user.Id,
            token,
            visitorId = user.Fingerprint
        }, Request.Scheme);
        await _emailSender.SendConfirmationLinkAsync(user, user.Email!, confirmationLink!);
        return Ok(new { Message = "Confirmation email sent successfully." });
    }

    [HttpPut("refresh-tokens")]
    public async Task<IActionResult> RefreshTokens([FromBody] RefreshTokenRequestDto dto)
    {
        if (string.IsNullOrEmpty(dto.AccessToken))
        {
            _logger.LogWarning("Access token missing during tokens refresh attempt. VisitorId: {VisitorId}", dto.VisitorId);
            return BadRequest(new { Message = "Access token is required." });
        }
        
        var principal = GetPrincipalFromExpiredToken(dto.AccessToken);
        if (principal == null)
        {
            _logger.LogWarning("Invalid access token. VisitorId: {VisitorId}", dto.VisitorId);
            return Unauthorized(new { Message = "Invalid access token." });
        }

        var userEmail = principal.Identity?.Name;
        if (string.IsNullOrEmpty(userEmail))
        {
            _logger.LogWarning("Invalid access token payload. VisitorId: {VisitorId}", dto.VisitorId);
            return Unauthorized(new { Message = "Invalid access token." });
        }

        var session = await _db.RefreshSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.RefreshToken == dto.RefreshToken && s.User.Email == userEmail);
        if (session == null || session.ExpiresIn < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            _logger.LogWarning("Invalid or expired refresh token. VisitorId: {VisitorId}", dto.VisitorId);
            return Unauthorized(new { Message = "Invalid or expired refresh token." });
        }

        if (dto.VisitorId != session.Fingerprint)
        {
            Log.Warning("Forged visitor Id {VisitorId}", dto.VisitorId);
            return BadRequest(new { Message = "Forged visitor ID."});
        }
        var user = await _userManager.FindByEmailAsync(userEmail);
        if (user == null)
        {
            _logger.LogWarning("Refresh tokens requested for non-existent user. VisitorId: {VisitorId}", dto.VisitorId);
            return Unauthorized(new { Message = "User is not found." });
        }

        var newAccessToken = GenerateAccessToken(user);
        var newRefreshToken = GenerateRefreshToken();
        session.RefreshToken = newRefreshToken;
        session.ExpiresIn = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds();
        session.UA = Request.Headers.UserAgent.ToString();
        session.Ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        _db.RefreshSessions.Update(session);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Tokens refreshed successfully. UserId: {UserId}", user.Id);
        return Ok(new
        {
            access_token = newAccessToken,
            refresh_token = newRefreshToken
        });
    }
    
    [NonAction]
    private string GenerateAccessToken(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.UserName!),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim(ClaimTypes.Role, "User")
        };

        var jwt = new JwtSecurityToken(
            issuer: AuthOptions.ISSUER,
            audience: AuthOptions.AUDIENCE,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(AuthOptions.LIFETIME),
            signingCredentials: new SigningCredentials(AuthOptions.GetSymmetricSecurityKey(), SecurityAlgorithms.HmacSha256)
        );
        _logger.LogDebug("Jwt(Access token) generated. UserId: {UserId}", user.Id);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    [NonAction]
    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        _logger.LogDebug("Refresh token generated.");
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
            _logger.LogDebug("Validating jwt token...");
            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
            if (securityToken is JwtSecurityToken jwtToken &&
                jwtToken.Header.Alg == SecurityAlgorithms.HmacSha256)
            {
                return principal;
            }
        }
        catch(Exception ex)
        {
            _logger.LogError("Error while validating jwt token. Error message: {ErrorMessage}", ex.Message);
        }
        return null;
    }
}