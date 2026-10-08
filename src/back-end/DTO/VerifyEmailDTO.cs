using System.ComponentModel.DataAnnotations;

namespace back_end.DTO.Auth;

public sealed class VerifyEmailDTO
{
    [Required]
    public string Token { get; set; } = "";
}