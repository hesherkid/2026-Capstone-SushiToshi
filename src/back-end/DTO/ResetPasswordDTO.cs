using System.ComponentModel.DataAnnotations;

namespace back_end.DTO.Auth;

public sealed class ResetPasswordDTO
{
    [Required]
    public string Token { get; set; } = "";

    [Required, MinLength(8)]
    public string NewPassword { get; set; } = "";
}