using System.Net;
using System.Text.Encodings.Web;
using back_end.Configurations;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace back_end.Services.Email;

public sealed class SendGridEmailService : IEmailService
{
    private readonly ISendGridClient _client;
    private readonly SendGridSettings _settings;
    private readonly ILogger<SendGridEmailService> _logger;

    public SendGridEmailService(
        ISendGridClient client,
        IOptions<SendGridSettings> settings,
        ILogger<SendGridEmailService> logger)
    {
        _client = client;
        _settings = settings.Value;
        _logger = logger;
    }

    public Task SendEmailVerificationAsync(
        string toEmail,
        string verificationUrl,
        CancellationToken ct = default)
    {
        var safeUrl = HtmlEncoder.Default.Encode(verificationUrl);

        return SendAsync(
            toEmail,
            "Verify your Sushi Toshi email",
            $"Verify your email: {verificationUrl}",
            $"""
            <h2>Welcome to Sushi Toshi!</h2>
            <p>Please confirm your email address.</p>
            <p><a href="{safeUrl}">Verify Email</a></p>
            <p>If you did not create an account, ignore this email.</p>
            """,
            ct);
    }

    public Task SendPasswordResetAsync(
        string toEmail,
        string resetUrl,
        CancellationToken ct = default)
    {
        var safeUrl = HtmlEncoder.Default.Encode(resetUrl);

        return SendAsync(
            toEmail,
            "Reset your Sushi Toshi password",
            $"Reset your password: {resetUrl}",
            $"""
            <h2>Password Reset</h2>
            <p>We received a request to reset your password.</p>
            <p><a href="{safeUrl}">Reset Password</a></p>
            <p>If you did not request this, ignore the email.</p>
            """,
            ct);
    }

    private async Task SendAsync(
        string recipient,
        string subject,
        string plainText,
        string html,
        CancellationToken ct)
    {
        var message = MailHelper.CreateSingleEmail(
            new EmailAddress(
                _settings.FromEmail,
                _settings.FromName),
            new EmailAddress(recipient),
            subject,
            plainText,
            html);

        var response = await _client.SendEmailAsync(message, ct);

        if (response.StatusCode != HttpStatusCode.Accepted)
        {
            _logger.LogError(
                "SendGrid rejected email. Status: {Status}",
                response.StatusCode);

            throw new InvalidOperationException(
                "Email provider rejected the message.");
        }
    }
}