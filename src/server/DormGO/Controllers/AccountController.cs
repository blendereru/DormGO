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
    [HttpPost("signup")]
    public async Task<IActionResult> Register(UserRequestDto dto)
    {
        if (string.IsNullOrEmpty(dto.Password))
        {
            var sanitizedVisitorId = _inputSanitizer.Sanitize(dto.VisitorId);
            _logger.LogInformation("Password not provided during user registration. VisitorId: {VisitorId}", sanitizedVisitorId);
            ModelState.AddModelError(nameof(dto.Password), "The Password field is required");
            return ValidationProblem(ModelState);
        }
        var user = dto.Adapt<ApplicationUser>();
        user.RegistrationDate = DateTime.UtcNow;

        var result = await _userManager.CreateAsync(user, dto.Password);

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
        var profileUrl = Url.Action("GetUserProfile", "Profile", new { email = user.Email });
        var responseDto = user.Adapt<UserResponseDto>();
        return Created(profileUrl, responseDto);
    }

    [HttpPost("signin")]
    public async Task<IActionResult> Login(UserRequestDto dto)
    {
        var sanitizedVisitorId = _inputSanitizer.Sanitize(dto.VisitorId);
        if (string.IsNullOrEmpty(dto.Password))
        {
            _logger.LogInformation("Password not provided during login. VisitorId: {VisitorId}", sanitizedVisitorId);
            ModelState.AddModelError(nameof(dto.Password), "The password field is required.");
            return ValidationProblem(ModelState);
        }
        var user = await _db.Users.Include(u => u.RefreshSessions).FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
        {
            _logger.LogWarning("Invalid login attempt. VisitorId: {VisitorId}", sanitizedVisitorId);
            var problem = new ProblemDetails
            {
                Title = "Invalid credentials",
                Type = "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "Invalid email or password."
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
            Fingerprint = dto.VisitorId,
            UA = Request.Headers.UserAgent.ToString(),
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ExpiresIn = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds()
        };
        _db.RefreshSessions.Add(session);
        await _db.SaveChangesAsync();
        _logger.LogInformation("User successfully logged in. UserId: {UserId}", user.Id);
        var responseDto = new RefreshTokenResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
        return Ok(responseDto);
    }
    
    [HttpPost("password/reset/request")]
    public async Task<IActionResult> RequestPasswordReset(UserRequestDto requestDto)
    {
        var user = await _userManager.FindByEmailAsync(requestDto.Email);
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
    
    [HttpGet("email/change/confirm")]
    public async Task<IActionResult> UpdateEmail(string userId, string newEmail, string token)
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
    
    [HttpGet("password/reset/validate")]
    public async Task<IActionResult> ValidatePasswordReset(string userId, string token)
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
    [HttpDelete("signout")]
    public async Task<IActionResult> Logout(RefreshTokenRequestDto dto)
    {
        var session = await _db.RefreshSessions.FirstOrDefaultAsync(x => x.RefreshToken == dto.RefreshToken);
        var sanitizedVisitorId = _inputSanitizer.Sanitize(dto.VisitorId);
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
    [HttpGet("email/confirm")]
    public async Task<IActionResult> ConfirmEmail(string userId, string token, string visitorId)
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
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            ExpiresIn = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds()
        };
        _db.RefreshSessions.Add(session);
        await _db.SaveChangesAsync();
        var tokensDto = new RefreshTokenResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };
        await _userHubNotificationService.NotifyEmailConfirmedAsync(user, tokensDto);
        return Ok("good on you");
    }

    [HttpPost("email/confirmation/resend")]
    public async Task<IActionResult> ResendConfirmationEmail(UserRequestDto requestDto)
    {
        var sanitizedVisitorId = _inputSanitizer.Sanitize(requestDto.VisitorId);
        var user = await _userManager.FindByEmailAsync(requestDto.Email);
        if (user != null)
        {
            if (!await _userManager.IsEmailConfirmedAsync(user))
            {
                user.Fingerprint = requestDto.VisitorId;
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
    [HttpPut("tokens/refresh")]
    public async Task<IActionResult> RefreshTokens(RefreshTokenRequestDto dto)
    {
        var sanitizedVisitorId = _inputSanitizer.Sanitize(dto.VisitorId);
        if (string.IsNullOrEmpty(dto.AccessToken))
        {
            _logger.LogWarning("Access token missing during tokens refresh attempt. VisitorId: {VisitorId}", sanitizedVisitorId);
            ModelState.AddModelError(nameof(dto.AccessToken), "The Access token field is required");
            return ValidationProblem(ModelState);
        }

        var principal = await _tokensProvider.GetPrincipalFromExpiredTokenAsync(dto.AccessToken);
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
            .FirstOrDefaultAsync(s => s.RefreshToken == dto.RefreshToken && s.User.Email == userEmail);
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

        if (dto.VisitorId != session.Fingerprint)
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
        session.Ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        _db.RefreshSessions.Update(session);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Tokens refreshed successfully. UserId: {UserId}", user.Id);
        var responseDto = new RefreshTokenResponseDto()
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken
        };
        return Ok(responseDto);
    }
}