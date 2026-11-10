using System.Text.Json.Serialization;

namespace back_end.Dtos.Auth
{
    public class LoginDto
    {
        [JsonPropertyName("email")]
        public string? UserEmail { get; set; }

        [JsonPropertyName("password")]
        public string? UserPassword { get; set; }
    }

    public class RegisterDto
    {
        [JsonPropertyName("email")]
        public string? UserEmail { get; set; }

        [JsonPropertyName("password")]
        public string? UserPassword { get; set; }

        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }
    }

    public class GoogleTokenDto
    {
        [JsonPropertyName("id_token")]
        public string IdToken { get; set; } = string.Empty;
    }
}