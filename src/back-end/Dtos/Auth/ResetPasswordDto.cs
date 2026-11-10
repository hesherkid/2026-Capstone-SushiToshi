using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace back_end.Dtos.Auth;

public sealed class ResetPasswordDto
{
    [Required]
    [JsonPropertyName("token")]
    public string Token { get; set; } = "";

    [Required, MinLength(8), MaxLength(200)]
    [JsonPropertyName("new_password")]
    public string NewPassword { get; set; } = "";
}