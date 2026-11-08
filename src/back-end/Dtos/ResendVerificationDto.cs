using System.ComponentModel.DataAnnotations;

namespace back_end.Dtos;

public sealed class ResendVerificationDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";
}