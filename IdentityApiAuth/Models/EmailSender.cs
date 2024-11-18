using System.Net;
using System.Net.Mail;

namespace IdentityApiAuth.Models;

public class EmailSender : IEmailSender
{
    private readonly IConfiguration _conf;
    public EmailSender(IConfiguration conf)
    {
        _conf = conf;
    }
    public async Task SendEmailAsync(string to, string subject, string body, bool isBodyHtml = false)
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
}