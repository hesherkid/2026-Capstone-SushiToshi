using System.ComponentModel.DataAnnotations;

namespace back_end.Dtos;

public sealed class ResetPasswordDto
{
    [Required]
    public string Token { get; set; } = "";

    [Required, MinLength(8), MaxLength(200)]
    public string NewPassword { get; set; } = "";
}