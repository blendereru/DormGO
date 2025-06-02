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
        <p><a href='{HtmlEncoder.Default.Encode(resetLink)}'>Reset Password</a></p>
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
        <p><a href='{HtmlEncoder.Default.Encode(changeLink)}'>Change Email</a></p>
        <p>If you did not request this, please ignore this email.</p>";
        await SendEmailAsync(email, subject, body, true);
        _logger.LogInformation("Email change link sent to user. UserId: {UserId}", user.Id);
    }
}