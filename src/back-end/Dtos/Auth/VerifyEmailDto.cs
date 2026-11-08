using System.ComponentModel.DataAnnotations;

namespace back_end.Dtos.Auth;

public sealed class VerifyEmailDto
{
    [Required]
    public string Token { get; set; } = "";
}