

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using back_end.domain;
using back_end.domain.Entities;
using back_end.domain.enums;

namespace back_end.Dtos
{
    public class StaffResponseDto
    {
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }
        [JsonPropertyName("is_email_confirmed")]
        public bool IsEmailConfirmed { get; set; }

        [JsonPropertyName("last_transaction_at")]
        public DateTime LastTransactionAt { get; set; }
        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }
        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("role")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UserRoles Role { get; set; }

        [JsonPropertyName("status")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UserStatus Status { get; set; }
        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        // The location they're working at now; null when they have none
        [JsonPropertyName("location_id")]
        public int? LocationId { get; set; }

        // The locations an admin has assigned them to
        [JsonPropertyName("location_ids")]
        public List<int> LocationIds { get; set; } = new();

        [JsonPropertyName("locations")]
        public List<string> Locations { get; set; } = new();
    }

    public class StaffUserUpdateDto
    {
        [MaxLength(255), EmailAddress]
        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [MaxLength(100)]
        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [MaxLength(100)]
        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }

        // allow moving between Staff/Admin only
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UserRoles? Role { get; set; }
        
        [JsonPropertyName("location")]
        public string? Location { get; set; }
    }

    public class CreateStaffDto
    {
        [MaxLength(255), EmailAddress]
        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [MaxLength(100)]
        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }
        [MinLength(8), MaxLength(200)]
        [JsonPropertyName("password")]
        public string? Password { get; set; }

        [MaxLength(100)]
        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }

        // allow moving between Staff/Admin only
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UserRoles? Role { get; set; }

        [JsonPropertyName("status")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UserStatus Status { get; set; }

        [JsonPropertyName("location_id")]
        public int? LocationId { get; set; }

        // Locations staff may work at; location_id, if given, is the one they start at
        [JsonPropertyName("location_ids")]
        public List<int>? LocationIds { get; set; }
    }

    public class StaffMemberChangePasswordDto
    {
        [Required, MinLength(8), MaxLength(200)]
        [JsonPropertyName("password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required, MinLength(8), MaxLength(200)]
        [JsonPropertyName("current_password")]
        public string CurrentPassword { get; set; } = string.Empty;
    }
    
    public class StaffMemberUpdateDto
    {
                [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }
        [JsonPropertyName("is_email_confirmed")]
        public bool IsEmailConfirmed { get; set; }

        [JsonPropertyName("last_transaction_at")]
        public DateTime LastTransactionAt { get; set; }
        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }
        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("role")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UserRoles Role { get; set; }

        [JsonPropertyName("status")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UserStatus Status { get; set; }
        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        // Locations staff may work at. Null leaves them unchanged
        [JsonPropertyName("location_ids")]
        public List<int>? LocationIds { get; set; }
    }

    // The signed-in user's locations, for the app bar's location picker
    public class MyLocationsDto
    {
        // The location they're working at now; null when they have none
        [JsonPropertyName("current_location")]
        public LocationOptionDto? CurrentLocation { get; set; }

        // Staff: the locations an admin assigned them. Admins: every location
        [JsonPropertyName("locations")]
        public List<LocationOptionDto> Locations { get; set; } = new();

        // Admins can work at any location
        [JsonPropertyName("all_locations")]
        public bool AllLocations { get; set; }
    }

    public class LocationOptionDto
    {
        [JsonPropertyName("location_id")]
        public int LocationId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}