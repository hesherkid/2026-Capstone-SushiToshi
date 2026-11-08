using System.ComponentModel.DataAnnotations;

namespace back_end.DTO.Auth;

public sealed class ForgotPasswordDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
}