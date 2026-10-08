namespace back_end.Services.Email;

public interface IEmailService
{
    Task SendEmailVerificationAsync(
        string toEmail,
        string verificationUrl,
        CancellationToken ct = default);

    Task SendPasswordResetAsync(
        string toEmail,
        string resetUrl,
        CancellationToken ct = default);
}