using System.ComponentModel.DataAnnotations;

namespace back_end.DTO.Auth;

public sealed class ResetPasswordDto
{
    [Required]
    public string Token { get; set; } = "";

    [Required, MinLength(8), MaxLength(200)]
    public string NewPassword { get; set; } = "";
}