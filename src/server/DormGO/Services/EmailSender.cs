using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;
using DormGO.Models;
using Microsoft.AspNetCore.Identity;

namespace DormGO.Services;

public class EmailSender : IEmailSender<ApplicationUser>
{
    private readonly IConfiguration _conf;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IConfiguration conf, ILogger<EmailSender> logger)
    {
        _conf = conf;
        _logger = logger;
    }
    private async Task SendEmailAsync(string to, string subject, string body, bool isBodyHtml = false)
    {
        var mailServer = _conf["EmailSettings:MailServer"]!;
        var fromEmail = _conf["EmailSettings:FromEmail"]!;
        var password = _conf["EmailSettings:Password"]!;
        var port = int.Parse(_conf["EmailSettings:MailPort"]!);
        using var smtpClient = new SmtpClient(mailServer, port)
        {
            Credentials = new NetworkCredential(fromEmail, password),
            EnableSsl = true
        };
        var mailMessage = new MailMessage(fromEmail, to, subject, body) { IsBodyHtml = isBodyHtml };
        await smtpClient.SendMailAsync(mailMessage);
    }
    public async Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var subject = "Confirm your email";
        var body = $"Please confirm your email by <a href='{HtmlEncoder.Default.Encode(confirmationLink)}'>clicking here</a>.";
        await SendEmailAsync(email, subject, body, isBodyHtml: true);
        _logger.LogInformation("Email confirmation link sent to user. UserId: {UserId}", user.Id);
    }

    public async Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        await SendEmailAsync(email, "Reset your password",
            "<html lang=\"en\"><head></head><body>Please reset your password " +
            $"using the following code:<br>{resetCode}</body></html>");
        _logger.LogInformation("Password reset code sent to user. UserId: {UserId}", user.Id);
    }
    public async Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var subject = "Reset Your Password";
        var body = $@"
        <p>Hi {user.UserName},</p>
        <p>You requested to reset your password. Click the link below to reset it:</p>
        <p><a href='{resetLink}'>Reset Password</a></p>
        <p>If you did not request this, please ignore this email.</p>";
        await SendEmailAsync(email, subject, body, true);
        _logger.LogInformation("Password reset link sent to user. UserId: {UserId}", user.Id);
    }

    public async Task SendEmailChangeLinkAsync(ApplicationUser user, string email, string changeLink)
    {
        var subject = "Change your email";
        var body = $@"
        <p>Hi {user.UserName},</p>
        <p>You requested to change your email. Click the link below to change it:</p>
        <p><a href='{changeLink}'>Change Email</a></p>
        <p>If you did not request this, please ignore this email.</p>";
        await SendEmailAsync(email, subject, body, true);
        _logger.LogInformation("Email change link sent to user. UserId: {UserId}", user.Id);
    }
    /*private async Task SendConfirmationEmailAsync(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user, nameof(user));
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationLink = Url.Action(
            "ConfirmEmail",
            "Account",
            new { userId = user.Id, token, visitorId = user.Fingerprint },
            protocol: HttpContext.Request.Scheme
        );
        var body = $"Please confirm your email by <a href='{HtmlEncoder.Default.Encode(confirmationLink)}'>clicking here</a>.";
        await _emailSender.SendEmailAsync(user.Email!, "Confirm your email", body, true);
    }
    public async Task SendResetPasswordAsync(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user, nameof(user));
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetLink = Url.Action(
            "ResetPassword",
            "Account",
            new { userId = user.Id, token = token },
            protocol: HttpContext.Request.Scheme);
        var emailSubject = "Reset Your Password";
        var emailBody = $@"
        <p>Hi {user.UserName},</p>
        <p>You requested to reset your password. Click the link below to reset it:</p>
        <p><a href='{resetLink}'>Reset Password</a></p>
        <p>If you did not request this, please ignore this email.</p>";
        await _emailSender.SendEmailAsync(user.Email!, emailSubject, emailBody, true);
    }
    
    public async Task SendChangeEmailAsync(ApplicationUser user, string newEmail)
    {
        ArgumentNullException.ThrowIfNull(user, nameof(user));
        var token = await _userManager.GenerateChangeEmailTokenAsync(user, newEmail);
        var changeLink = Url.Action("UpdateEmail", "Account", new { userId = user.Id, newEmail, token },
            protocol: HttpContext.Request.Scheme);
        var emailSubject = "Change your email";
        var emailBody = $@"
        <p>Hi {user.UserName},</p>
        <p>You requested to change your email. Click the link below to change it:</p>
        <p><a href='{changeLink}'>Change email</a></p>
        <p>If you did not request this, please ignore this email.</p>";
        await _emailSender.SendEmailAsync(newEmail, emailSubject, emailBody, true);
    }*/
}