using GraNAS.Notifications.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Utils;

namespace GraNAS.Notifications.Services.Implementations;

public class MailKitEmailSender : IEmailSender
{
    private readonly SmtpOptions _opts;
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(IOptions<SmtpOptions> opts, ILogger<MailKitEmailSender> logger)
    {
        _opts = opts.Value;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, string textBody, CancellationToken ct = default)
    {
        _logger.LogDebug("SMTP: sending to {Recipient} subject={Subject}", to, subject);

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_opts.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.MessageId = MimeUtils.GenerateMessageId();

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = textBody
        };
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            var secureSocketOptions = _opts.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            await client.ConnectAsync(_opts.Host, _opts.Port, secureSocketOptions, ct);

            if (!string.IsNullOrEmpty(_opts.Username))
                await client.AuthenticateAsync(_opts.Username, _opts.Password, ct);

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP: send failed for {Recipient}", to);
            throw;
        }
    }
}
