using System.ComponentModel.DataAnnotations;

namespace back_end.Configurations;

public sealed class AuthEmailSettings
{
    public const string SectionName = "AuthEmail";

    [Required, Url]
    public string FrontendBaseUrl { get; set; } = "";

    [Range(1, 10080)]
    public int VerificationExpiryMinutes { get; set; } = 1440;

    [Range(1, 1440)]
    public int PasswordResetExpiryMinutes { get; set; } = 30;
}