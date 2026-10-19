using System.Text.Json.Serialization;

namespace back_end.DTO.Auth
{
    public class LoginDTO
    {
        [JsonPropertyName("email")]
        public string? UserEmail { get; set; }

        [JsonPropertyName("password")]
        public string? UserPassword { get; set; }
    }

    public class RegisterDTO
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

    public class GoogleTokenDTO
    {
        public string IdToken { get; set; } = string.Empty;
    }
}