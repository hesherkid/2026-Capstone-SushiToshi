

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using back_end.domain;
using back_end.domain.Entities;
using back_end.domain.enums;

namespace back_end.Dtos
{
    public class StaffResponseDto
    {
        public DateTime CreatedAt { get; set; }

        public string? Email { get; set; }
        public bool IsEmailConfirmed { get; set; }

        public DateTime LastTransactionAt { get; set; }
        public string? LastName { get; set; }
        public string? FirstName { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UserRoles Role { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UserStatus Status { get; set; }
        public int UserId { get; set; }

        public string? Location { get; set; }

        // The location they're working at now; null when they have none
        public int? LocationId { get; set; }

        // The locations an admin has assigned them to
        public List<int> LocationIds { get; set; } = new();

        public List<string> Locations { get; set; } = new();
    }

    public class StaffUserUpdateDto
    {
        [MaxLength(255), EmailAddress]
        public string? Email { get; set; }

        [MaxLength(100)]
        public string? FirstName { get; set; }

        [MaxLength(100)]
        public string? LastName { get; set; }

        // allow moving between Staff/Admin only
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UserRoles? Role { get; set; }
        
        public string? Location { get; set; }
    }

    public class CreateStaffDto
    {
        [MaxLength(255), EmailAddress]
        public string? Email { get; set; }

        [MaxLength(100)]
        public string? FirstName { get; set; }
        [MinLength(8), MaxLength(200)]
        public string? Password { get; set; }

        [MaxLength(100)]
        public string? LastName { get; set; }

        // allow moving between Staff/Admin only
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UserRoles? Role { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UserStatus Status { get; set; }

        public int? LocationId { get; set; }

        // Locations staff may work at; location_id, if given, is the one they start at
        public List<int>? LocationIds { get; set; }
    }

    public class StaffMemberChangePasswordDto
    {
        [Required, MinLength(8), MaxLength(200)]
        [JsonPropertyName("password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required, MinLength(8), MaxLength(200)]
        public string CurrentPassword { get; set; } = string.Empty;
    }
    
    public class StaffMemberUpdateDto
    {
        public DateTime CreatedAt { get; set; }

        public string? Email { get; set; }
        public bool IsEmailConfirmed { get; set; }

        public DateTime LastTransactionAt { get; set; }
        public string? LastName { get; set; }
        public string? FirstName { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UserRoles Role { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UserStatus Status { get; set; }
        public int UserId { get; set; }

        public string? Location { get; set; }

        // Locations staff may work at. Null leaves them unchanged
        public List<int>? LocationIds { get; set; }
    }

    // The signed-in user's locations, for the app bar's location picker
    public class MyLocationsDto
    {
        // The location they're working at now; null when they have none
        public LocationOptionDto? CurrentLocation { get; set; }

        // Staff: the locations an admin assigned them. Admins: every location
        public List<LocationOptionDto> Locations { get; set; } = new();

        // Admins can work at any location
        public bool AllLocations { get; set; }
    }

    public class LocationOptionDto
    {
        public int LocationId { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}