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

    // TODO: nothing in the code currently uses GoogleLoginDTO, consider removing it if not needed.
    public class GoogleLoginDTO
    {
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Sub { get; set; } = string.Empty; // Google's unique user ID
    }
    public class GoogleTokenDTO
    {
        public string IdToken { get; set; } = string.Empty;
    }
}