namespace IdentityApiAuth.Models;

public interface IEmailSender
{
    Task SendEmailAsync(string to, string subject, string body, bool isBodyHtml = false);
}
