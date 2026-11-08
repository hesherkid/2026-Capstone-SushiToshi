using System.ComponentModel.DataAnnotations;

namespace back_end.Dtos.Auth;

public sealed class ForgotPasswordDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
}