using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using DormGO.Data;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
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
    public async Task<IActionResult> Register([FromBody] UserRequestDto dto)
    {
        Log.Information("Register: Signup request received for email {Email}.", dto.Email);
        if (string.IsNullOrEmpty(dto.Password))
        {
            Log.Information("Register: Failed to create the user as the password is not provided");
            return BadRequest(new { Message =  "Couldn't register user. Password is not provided"});
        }
        var user = _mapper.Map<ApplicationUser>(dto);
        user.RegistrationDate = DateTime.UtcNow;

        var result = await _userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                Log.Warning("Register: Failed to create user {Email}. Error: {Error}", dto.Email, error.Description);
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return BadRequest(ModelState);
        }

        Log.Information("Register: User {Email} registered successfully. Sending confirmation email.", dto.Email);
        await SendConfirmationEmailAsync(user);

        return Ok(new { Message = "User registered successfully. Email confirmation is pending." });
    }

    [HttpPost("signin")]
    public async Task<IActionResult> Login([FromBody] UserRequestDto dto)
    {
        Log.Information("Login: Signin request received for email {Email}.", dto.Email);
        if (string.IsNullOrEmpty(dto.Password))
        {
            Log.Information("Login: User {Email} didn't provide his password", dto.Email);
            return BadRequest(new { Message = "Login failed. Password is not provided" });
        }
        var user = await _db.Users.Include(u => u.RefreshSessions).FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user == null)
        {
            Log.Warning("Login: Failed attempt. User {Email} not found.", dto.Email);
            return Unauthorized(new { Message = "Invalid email" });
        }
        if (!user.EmailConfirmed)
        {
            Log.Warning("Login: User {Email} attempted login but email is not confirmed.", dto.Email);
            return BadRequest(new { Message = "Email is not confirmed. Please check your email for the confirmation link." });
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, dto.Password);

        if (!isPasswordValid)
        {
            Log.Warning("Login: Invalid password attempt for email {Email}.", dto.Email);
            return Unauthorized(new { Message = "Invalid password." });
        }
        if (user.RefreshSessions.Count >= 5)
        {
            Log.Information("Login: Clearing excess refresh sessions for user {Email}.", dto.Email);
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

        Log.Information("Login: User {Email} logged in successfully.", dto.Email);

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
        Log.Information("ForgotPassword: Request received for email {Email}.", requestDto.Email);

        var user = await _userManager.FindByEmailAsync(requestDto.Email);
        if (user == null)
        {
            Log.Warning("ForgotPassword: User {Email} not found.", requestDto.Email);
            return NotFound(new { Message = "User not found."});
        }

        Log.Information("ForgotPassword: Sending forgot password email to {Email}.", requestDto.Email);
        await SendForgotPasswordEmailAsync(user);

        return Ok(new { Message = "Forgot password email sent successfully."});
    }

    [HttpGet("reset-password")]
    public async Task<IActionResult> ResetPassword(string userId, string token, [FromServices] IHubContext<UserHub> hub)
    {
        Log.Information("ResetPassword: Request to validate reset password link for user {UserId}.", userId);

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            Log.Warning("ResetPassword: Invalid or expired reset link for user {UserId}.", userId);
            return BadRequest(new { Message = "The link is invalid or expired."});
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            Log.Warning("ResetPassword: User {UserId} not found.", userId);
            return NotFound(new { Message = "The user is not found." });
        }

        var isTokenValid = await _userManager.VerifyUserTokenAsync(
            user,
            _userManager.Options.Tokens.PasswordResetTokenProvider,
            "ResetPassword",
            token
        );

        if (!isTokenValid)
        {
            Log.Warning("ResetPassword: Invalid or expired token for user {UserId}.", userId);
            return BadRequest(new { Message = "The link is invalid or expired."});
        }
        var connections = await _db.UserConnections
            .Where(c => c.UserId == userId)
            .Select(c => c.ConnectionId)
            .ToListAsync();
        if (connections.Any())
        {
            await hub.Clients.Clients(connections).SendAsync("PasswordResetLinkValidated", new
            {
                Message = "Your password reset link has been verified.",
                Email = user.Email!,
                Token = token,
                Timestamp = DateTime.UtcNow
            });
            Log.Information("ResetPassword: Notification sent to user {UserId} for valid reset link.", userId);
        }
        else
        {
            Log.Warning("ResetPassword: No active connections found for user {UserId}.", userId);
        }
        return Ok(new
        {
            Message = "The link is valid. You can now reset your password.",
        });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequestDto model)
    {
        Log.Information("ResetPassword: Attempt to reset password for email {Email}.", model.Email);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            Log.Warning("ResetPassword: User {Email} not found.", model.Email);
            return NotFound(new { Message = "The user is not found."});
        }

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);

        if (!result.Succeeded)
        {
            Log.Warning("ResetPassword: Failed to reset password for {Email}. Errors: {Errors}",
                model.Email, result.Errors.Select(e => e.Description));
            return BadRequest(result.Errors);
        }

        Log.Information("ResetPassword: Password reset successfully for {Email}.", model.Email);
        return Ok(new { Message = "Your password has been reset successfully." });
    }
    [HttpDelete("signout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto dto)
    {
        Log.Information("Logout initiated for refresh token: {RefreshToken}", dto.RefreshToken);

        var session = await _db.RefreshSessions.FirstOrDefaultAsync(x => x.RefreshToken == dto.RefreshToken);

        if (session == null)
        {
            Log.Warning("Logout failed. Refresh token not found: {RefreshToken}", dto.RefreshToken);
            return BadRequest(new { Message = "The refresh token is not found"});
        }
        _db.RefreshSessions.Remove(session);
        await _db.SaveChangesAsync();
        Log.Information("Logout successful. Refresh token removed: {RefreshToken}", dto.RefreshToken);
        return Ok(new { Message = "The refresh token was successfully removed"});
    }
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(string userId, string token, string visitorId,
        [FromServices] IHubContext<UserHub> hub)
    {
        Log.Information("Email confirmation initiated for UserId: {UserId}, Token: {Token}, VisitorId: {VisitorId}", userId, token, visitorId);
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            Log.Warning("Email confirmation failed. Invalid or missing parameters. UserId: {UserId}, Token: {Token}", userId, token);
            return BadRequest(new { Message = "The link is invalid or expired."});
        }
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            Log.Warning("Email confirmation failed. User not found: {UserId}", userId);
            return NotFound(new { Message = "The user is not found"});
        }
        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            Log.Warning("Email confirmation failed for UserId: {UserId}. Errors: {Errors}", userId, result.Errors);
            return BadRequest(result);
        }
        Log.Information("Email confirmed successfully for UserId: {UserId}", userId);
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
        Log.Information("New session created for UserId: {UserId} with RefreshToken: {RefreshToken}", userId, refreshToken);
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
        }

        return Ok(new { Message = "Email confirmed successfully" });
    }

    [HttpPost("resend-confirmation-email")]
    public async Task<IActionResult> ResendConfirmationEmail([FromBody] UserRequestDto requestDto)
    {
        Log.Information("Resending confirmation email for Email: {Email}", requestDto.Email);
        var user = await _userManager.FindByEmailAsync(requestDto.Email);
        if (user == null)
        {
            Log.Warning("Resend confirmation email failed. User not found: {Email}", requestDto.Email);
            return NotFound(new { Message = "User not found."});
        }
        if (await _userManager.IsEmailConfirmedAsync(user))
        {
            Log.Information("Resend confirmation email skipped. Email already confirmed: {Email}", requestDto.Email);
            return BadRequest(new { Message = "Email is already confirmed." });
        }
        user.Fingerprint = requestDto.VisitorId;
        await SendConfirmationEmailAsync(user);
        Log.Information("Resent confirmation email successfully for Email: {Email}", requestDto.Email);
        return Ok(new { Message = "Confirmation email sent successfully." });
    }

    [HttpPut("refresh-tokens")]
    public async Task<IActionResult> RefreshTokens([FromBody] RefreshTokenRequestDto dto)
    {
        Log.Information("Refresh token request initiated. AccessToken: {AccessToken}, RefreshToken: {RefreshToken}", dto.AccessToken, dto.RefreshToken);
        if (string.IsNullOrEmpty(dto.AccessToken))
        {
            Log.Warning("Refresh token request failed. Missing access token.");
            return BadRequest(new { Message = "Access token is required." });
        }

        var principal = GetPrincipalFromExpiredToken(dto.AccessToken);

        if (principal == null)
        {
            Log.Warning("Refresh token request failed. Invalid access token: {AccessToken}", dto.AccessToken);
            return Unauthorized(new { Message = "Invalid access token." });
        }

        var userEmail = principal.Identity?.Name;

        if (string.IsNullOrEmpty(userEmail))
        {
            Log.Warning("Refresh token request failed. Invalid access token payload.");
            return Unauthorized(new { Message = "Invalid access token." });
        }

        var session = await _db.RefreshSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.RefreshToken == dto.RefreshToken && s.User.Email == userEmail);

        if (session == null || session.ExpiresIn < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            Log.Warning("Refresh token request failed. Invalid or expired refresh token: {RefreshToken}", dto.RefreshToken);
            return Unauthorized(new { Message = "Invalid or expired refresh token." });
        }

        if (dto.VisitorId != session.Fingerprint)
        {
            Log.Warning("Refresh token request failed. Forged visitor ID detected for UserEmail: {UserEmail}", userEmail);
            return BadRequest(new { Message = "Forged visitor ID."});
        }
        var user = await _userManager.FindByEmailAsync(userEmail);
        if (user == null)
        {
            Log.Warning("Refresh token request failed. User not found: {UserEmail}", userEmail);
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
        Log.Information("Refresh tokens updated successfully for UserEmail: {UserEmail}. New RefreshToken: {RefreshToken}", userEmail, newRefreshToken);
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