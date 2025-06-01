using System.ComponentModel;
using DormGO.Data;
using DormGO.DTOs.RequestDTO;
using DormGO.DTOs.ResponseDTO;
using DormGO.Models;
using DormGO.Services;
using DormGO.Services.HubNotifications;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DormGO.Controllers;

[ApiController]
[Route("api")]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
public class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationContext _db;
    private readonly IEmailSender<ApplicationUser> _emailSender;
    private readonly IUserHubNotificationService _userHubNotificationService;
    private readonly ITokensProvider _tokensProvider;
    private readonly ILogger<AccountController> _logger;
    private readonly IInputSanitizer _inputSanitizer;

    public AccountController(UserManager<ApplicationUser> userManager, ApplicationContext db,
        IEmailSender<ApplicationUser> emailSender, ITokensProvider tokensProvider,
        IUserHubNotificationService userHubNotificationService,
        ILogger<AccountController> logger, IInputSanitizer inputSanitizer)
    {
        _userManager = userManager;
        _db = db;
        _emailSender = emailSender;
        _userHubNotificationService = userHubNotificationService;
        _tokensProvider = tokensProvider;
        _logger = logger;
        _inputSanitizer = inputSanitizer;
    }
    [EndpointSummary("Registers a user.")]
    [EndpointDescription("Intended to register a user to the system.")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created, "application/json")]
    [HttpPost("signup")]
    public async Task<IActionResult> Register(UserRegisterRequest registerRequest)
    {
        var user = registerRequest.Adapt<ApplicationUser>();
        user.RegistrationDate = DateTime.UtcNow;
        var result = await _userManager.CreateAsync(user, registerRequest.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }
            _logger.LogWarning("User registration failed. Errors: {Errors}", result.Errors.Select(e => e.Description));
            return ValidationProblem(ModelState);
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
        var profileUrl = Url.Action("GetUserProfile", "Profile", new { id = user.Id });
        var response = user.Adapt<UserResponse>();
        return Created(profileUrl, response);
    }
    [EndpointSummary("Logs in a user")]
    [EndpointDescription("Intended to log user in to the system")]
    [ProducesResponseType<RefreshTokensResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [HttpPost("signin")]
    public async Task<IActionResult> Login(UserLoginRequest loginRequest)
    {
        var sanitizedVisitorId = _inputSanitizer.Sanitize(loginRequest.VisitorId);
        string? detail;
        ApplicationUser? user;
        if (loginRequest.Email != null)
        {
            user = await _db.Users.Include(u => u.RefreshSessions).FirstOrDefaultAsync(u => u.Email == loginRequest.Email);
            detail = "Invalid email or password.";
        }
        else
        {
            if (loginRequest.Name == null)
            {
                _logger.LogWarning("User email and name not provided during login. VisitorId: {VisitorId}", sanitizedVisitorId);
                ModelState.AddModelError(nameof(loginRequest.Name), "The name field is required");
                ModelState.AddModelError(nameof(loginRequest.Email), "The email field is required");
                return ValidationProblem(ModelState);
            }
            user = await _db.Users.Include(u => u.RefreshSessions).FirstOrDefaultAsync(u => u.UserName == loginRequest.Name);
            detail = "Invalid username or password.";
        }
        if (user == null || !await _userManager.CheckPasswordAsync(user, loginRequest.Password))
        {
            _logger.LogWarning("Invalid login attempt. VisitorId: {VisitorId}", sanitizedVisitorId);
            var problem = new ProblemDetails
            {
                Title = "Invalid credentials",
                Type = "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
                Status = StatusCodes.Status401Unauthorized,
                Detail = detail
            };
            return Unauthorized(problem);
        }
        if (!user.EmailConfirmed)
        {
            _logger.LogWarning("User's email is not confirmed yet. Blocking user. UserId: {UserId}", user.Id);
            var problem = new ProblemDetails
            {
                Title = "Email not confirmed",
                Type = "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
                Status = StatusCodes.Status403Forbidden,
                Detail = "Email is not confirmed. Please check your email for the confirmation link.",
            };
            return StatusCode(StatusCodes.Status403Forbidden, problem);
        }
        
        if (user.RefreshSessions.Count >= 5)
        {
            _db.RefreshSessions.RemoveRange(user.RefreshSessions);
            var count = user.RefreshSessions.Count;
            await _db.SaveChangesAsync();
            _logger.LogInformation("User's refresh sessions exceeded the limit and were cleared. UserId: {UserId}, ClearedSessionsCount: {Count}", user.Id, count);
        }

        var accessToken = _tokensProvider.GenerateAccessToken(user);
        var refreshToken = _tokensProvider.GenerateRefreshToken();
        var session = new RefreshSession
        {
            UserId = user.Id,
            RefreshToken = refreshToken,
            Fingerprint = loginRequest.VisitorId,
            UA = Request.Headers.UserAgent.ToString(),
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            ExpiresIn = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds()
        };
        _db.RefreshSessions.Add(session);
        await _db.SaveChangesAsync();
        _logger.LogInformation("User successfully logged in. UserId: {UserId}", user.Id);
        var response = new RefreshTokensResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
        return Ok(response);
    }
    [EndpointSummary("Endpoint for password forgot")]
    [EndpointDescription("Intended to change the user password when it was forgotten")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpPost("password/forgot")]
    public async Task<IActionResult> ForgotPassword(PasswordForgotRequest passwordForgotRequest)
    {
        var user = await _userManager.FindByEmailAsync(passwordForgotRequest.Email);
        if (user != null)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetLink = Url.Action("ResetPassword", "Account", new
            {
                userId = user.Id,
                token
            }, protocol: Request.Scheme);
            await _emailSender.SendPasswordResetLinkAsync(user, user.Email!, resetLink!);
        }
        return NoContent();
    }
    [EndpointSummary("The link that confirms email change")]
    [EndpointDescription("User goes by this link when it was sent to new email to confirm it")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpGet("email/change/confirm")]
    public async Task<IActionResult> UpdateEmail([Description("User's id to validate")] string userId, [Description("New email to change the email for")] string newEmail, [Description("Token using which the link signature is validated")] string token)
    {
        var sanitizedUserId = _inputSanitizer.Sanitize(userId);
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(newEmail))
        { 
            _logger.LogWarning("Invalid or expired email change link. UserId: {UserId}", sanitizedUserId);
            var problem = new ProblemDetails
            {
                Title = "Invalid or expired link",
                Type = "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
                Status = StatusCodes.Status400BadRequest,
                Detail = "The link is invalid, expired, or missing required parameters."
            };
            return BadRequest(problem);
        }
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("Email change failed. User not found. UserId: {UserId}", sanitizedUserId);
            var problem = new ProblemDetails
            {
                Title = "User Not Found",
                Type = "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
                Status = StatusCodes.Status404NotFound,
                Detail = "The specified user could not be found."
            };
            return NotFound(problem);
        }
        var result = await _userManager.ChangeEmailAsync(user, newEmail, token);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }
            _logger.LogWarning("User email change failed. UserId: {UserId}. Errors: {Errors}", user.Id, result.Errors.Select(e => e.Description));
            return ValidationProblem(ModelState);
        }
        _logger.LogInformation("Email changed successfully. UserId: {UserId}", user.Id);
        await _userHubNotificationService.NotifyEmailChangedAsync(user);
        return NoContent();
    }
    [EndpointSummary("The link that confirms password reset")]
    [EndpointDescription("User goes by this link when it was sent to his email to confirm the password reset")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpGet("password/reset/validate")]
    public async Task<IActionResult> ValidatePasswordReset([Description("User's id to validate")] string userId, [Description("Token using which the link signature is validated")] string token)
    {
        var sanitizedUserId = _inputSanitizer.Sanitize(userId);
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Invalid or expired password reset link. UserId: {UserId}", sanitizedUserId);
            var problem = new ProblemDetails
            {
                Title = "Invalid or Expired Link",
                Type = "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
                Status = StatusCodes.Status400BadRequest,
                Detail = "The password reset link is invalid, expired, or missing required parameters."
            };
            return BadRequest(problem);
        }
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User not found during password reset. UserId: {UserId}", sanitizedUserId);
            var problem = new ProblemDetails
            {
                Title = "User Not Found",
                Type = "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
                Status = StatusCodes.Status404NotFound,
                Detail = "The specified user could not be found."
            };
            return NotFound(problem);
        }
        await _userHubNotificationService.NotifyPasswordResetLinkValidated(user);
        return NoContent();
    }
    [EndpointSummary("Request to reset password")]
    [EndpointDescription("Intended to set new password to user")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpPost("password/reset")]
    public async Task<IActionResult> ResetPassword(PasswordResetRequest passwordResetRequest)
    {
        var user = await _userManager.FindByEmailAsync(passwordResetRequest.Email);
        if (user != null)
        {
            var result = await _userManager.ResetPasswordAsync(user, passwordResetRequest.Token, passwordResetRequest.NewPassword);

            if (!result.Succeeded)
            {
                _logger.LogWarning("Password reset failed. UserId: {UserId}, Errors: {Errors}", user.Id, result.Errors.Select(e => e.Description));
                // ToDo: implement logic to monitor/fail after repeated failed attempts.
            }
            else
            {
                _logger.LogInformation("Password successfully reset. UserId: {UserId}", user.Id);
            }
        }
        else
        {
            _logger.LogWarning("Password reset requested for non-existent user.");
        }
        return NoContent();
    }
    [EndpointSummary("Log user out")]
    [EndpointDescription("Intended to log user out of the system, removing his current session")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpDelete("signout")]
    public async Task<IActionResult> Logout(UserLogoutRequest userLogoutRequest)
    {
        var session = await _db.RefreshSessions.FirstOrDefaultAsync(x => x.RefreshToken == userLogoutRequest.RefreshToken);
        var sanitizedVisitorId = _inputSanitizer.Sanitize(userLogoutRequest.VisitorId);
        if (session != null)
        {
            _db.RefreshSessions.Remove(session);
            await _db.SaveChangesAsync();
            _logger.LogInformation("User logged out successfully. VisitorId: {VisitorId}", sanitizedVisitorId);
        }
        else
        {
            _logger.LogWarning("Refresh session not found during logout. VisitorId: {VisitorId}", sanitizedVisitorId);
        }
        return NoContent();
    }
    [EndpointSummary("The link that confirms user email")]
    [EndpointDescription("User goes by this link when it was sent to his email to confirm it")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [HttpGet("email/confirm")]
    public async Task<IActionResult> ConfirmEmail([Description("User's id to validate")] string userId, [Description("Token using which the link signature is validated")] string token, [Description("User's fingerprint(device id)")] string visitorId)
    {
        var sanitizedUserId = _inputSanitizer.Sanitize(userId);
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Invalid or expired link parameters. UserId: {UserId}", sanitizedUserId);
            var problem = new ProblemDetails
            {
                Title = "Invalid or Expired Link",
                Type = "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
                Status = StatusCodes.Status400BadRequest,
                Detail = "The email confirmation link is invalid, expired, or missing required parameters."
            };
            return BadRequest(problem);
        }
        
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("Email confirmation requested for non-existent user. UserId: {UserId}", sanitizedUserId);
            var problem = new ProblemDetails
            {
                Title = "User Not Found",
                Type = "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
                Status = StatusCodes.Status404NotFound,
                Detail = "The specified user could not be found."
            };
            return NotFound(problem);
        }
        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }
            _logger.LogWarning("Email confirmation failed. UserId: {UserId}, Errors: {Errors}", user.Id, result.Errors.Select(e => e.Description));
            return ValidationProblem(ModelState);
        }
        _logger.LogInformation("Email successfully confirmed. UserId: {UserId}", user.Id);
        var accessToken = _tokensProvider.GenerateAccessToken(user);
        var refreshToken = _tokensProvider.GenerateRefreshToken();
        var session = new RefreshSession
        {
            UserId = user.Id,
            RefreshToken = refreshToken,
            Fingerprint = visitorId,
            UA = Request.Headers.UserAgent.ToString(),
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            ExpiresIn = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds()
        };
        _db.RefreshSessions.Add(session);
        await _db.SaveChangesAsync();
        var response = new RefreshTokensResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
        await _userHubNotificationService.NotifyEmailConfirmedAsync(user, response);
        return Ok("good on you");
    }
    [EndpointSummary("Resend email confirmation link")]
    [EndpointDescription("Intended to resend email confirmation link to user")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [HttpPost("email/confirmation/resend")]
    public async Task<IActionResult> ResendConfirmationEmail(EmailConfirmationResendRequest emailConfirmationResendRequest)
    {
        var sanitizedVisitorId = _inputSanitizer.Sanitize(emailConfirmationResendRequest.VisitorId);
        var user = await _userManager.FindByEmailAsync(emailConfirmationResendRequest.Email);
        if (user != null)
        {
            if (!await _userManager.IsEmailConfirmedAsync(user))
            {
                user.Fingerprint = emailConfirmationResendRequest.VisitorId;
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var confirmationLink = Url.Action("ConfirmEmail", "Account", new
                {
                    userId = user.Id,
                    token,
                    visitorId = user.Fingerprint
                },Request.Scheme);
                await _emailSender.SendConfirmationLinkAsync(user, user.Email!, confirmationLink!);
                _logger.LogInformation("Confirmation email resent. UserId: {UserId}", user.Id);
            }
            else
            {
                _logger.LogInformation("Email already confirmed. Skipping confirmation resend. UserId: {UserId}", user.Id);
            }
        }
        else
        {
            _logger.LogWarning("Email confirmation resend requested for non-existent user. VisitorId: {VisitorId}", sanitizedVisitorId);
        }
        return NoContent();
    }
    [EndpointSummary("Refresh tokens")]
    [EndpointDescription("Intended to update both access and refresh tokens of user once the token expired.")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<RefreshTokensResponse>(StatusCodes.Status200OK, "application/json")]
    [HttpPut("tokens/refresh")]
    public async Task<IActionResult> RefreshTokens(RefreshTokensRequest refreshTokensRequest)
    {
        var sanitizedVisitorId = _inputSanitizer.Sanitize(refreshTokensRequest.VisitorId);
        var principal = await _tokensProvider.GetPrincipalFromExpiredTokenAsync(refreshTokensRequest.AccessToken);
        if (principal == null)
        {
            _logger.LogWarning("Invalid access token. VisitorId: {VisitorId}", sanitizedVisitorId);
            var problem = new ProblemDetails
            {
                Title = "Invalid access token",
                Type = "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "The access token is invalid."
            };
            return Unauthorized(problem);
        }
        var userEmail = principal.Identity?.Name;
        if (string.IsNullOrEmpty(userEmail))
        {
            _logger.LogWarning("Invalid access token payload. VisitorId: {VisitorId}", sanitizedVisitorId);
            var problem = new ProblemDetails
            {
                Title = "Invalid token payload",
                Type = "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "The access token payload is invalid."
            };
            return Unauthorized(problem);
        }
        var session = await _db.RefreshSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.RefreshToken == refreshTokensRequest.RefreshToken && s.User.Email == userEmail);
        if (session == null || session.ExpiresIn < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            _logger.LogWarning("Invalid or expired refresh token. VisitorId: {VisitorId}", sanitizedVisitorId);
            var problem = new ProblemDetails
            {
                Title = "Invalid or expired credentials",
                Type = "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "The credentials provided are invalid or expired."
            };
            return Unauthorized(problem);
        }

        if (refreshTokensRequest.VisitorId != session.Fingerprint)
        {
            _logger.LogWarning("Forged visitor ID. VisitorId: {VisitorId}", sanitizedVisitorId);
            var problem = new ProblemDetails
            {
                Title = "Invalid credentials",
                Type = "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "The credentials provided are invalid."
            };
            return Unauthorized(problem);
        }
        var user = session.User;
        var newAccessToken = _tokensProvider.GenerateAccessToken(user);
        var newRefreshToken = _tokensProvider.GenerateRefreshToken();
        session.RefreshToken = newRefreshToken;
        session.ExpiresIn = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds();
        session.UA = Request.Headers.UserAgent.ToString();
        session.Ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        _db.RefreshSessions.Update(session);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Tokens refreshed successfully. UserId: {UserId}", user.Id);
        var response = new RefreshTokensResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken
        };
        return Ok(response);
    }
}