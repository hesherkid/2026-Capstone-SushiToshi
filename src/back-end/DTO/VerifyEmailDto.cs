using System.ComponentModel.DataAnnotations;

namespace back_end.DTO.Auth;

public sealed class VerifyEmailDto
{
    [Required]
    public string Token { get; set; } = "";
}