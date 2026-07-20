using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace AuthService.Services;

public interface IEmailService
{
    Task SendOtpAsync(string toEmail, string otp, string purpose);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendOtpAsync(string toEmail, string otp, string purpose)
    {
        var subject = purpose switch
        {
            "email_verification" => "Verify your Innovator account",
            "forgot_password" => "Reset your Innovator password",
            _ => "Innovator OTP"
        };

        var body = $"""
            <div style="font-family:sans-serif;max-width:480px;margin:auto">
              <h2 style="color:#1a1a2e">Innovator</h2>
              <p>Your one-time code is:</p>
              <h1 style="letter-spacing:8px;color:#6c63ff">{otp}</h1>
              <p>This code expires in <strong>10 minutes</strong>. Do not share it with anyone.</p>
            </div>
            """;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Innovator", _config["Email:From"]));
        message.To.Add(new MailboxAddress("", toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = body };

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(
                _config["Email:Host"],
                int.Parse(_config["Email:Port"] ?? "587"),
                SecureSocketOptions.StartTls);

            await client.AuthenticateAsync(_config["Email:Username"], _config["Email:Password"]);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OTP email to {Email}", toEmail);
            throw;
        }
    }
}
