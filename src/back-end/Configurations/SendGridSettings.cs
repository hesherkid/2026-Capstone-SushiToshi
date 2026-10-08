using System.ComponentModel.DataAnnotations;

namespace back_end.Configurations;

public sealed class SendGridSettings
{
    public const string SectionName = "SendGrid";

    [Required]
    public string ApiKey { get; set; } = "";

    [Required, EmailAddress]
    public string FromEmail { get; set; } = "";

    [Required]
    public string FromName { get; set; } = "";
}