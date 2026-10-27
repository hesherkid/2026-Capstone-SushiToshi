

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using back_end.domain;
using back_end.domain.Entities;
using back_end.domain.enums;

namespace back_end.DTO.StaffDTOs
{
    public class StaffResponseDTO
    {
        [JsonPropertyName("created_at")]
        public DateTime Created_At { get; set; }

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
        public int User_Id { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        // The location they're working at now; null when they have none
        [JsonPropertyName("location_id")]
        public int? Location_Id { get; set; }

        // The locations an admin has assigned them to
        [JsonPropertyName("location_ids")]
        public List<int> Location_Ids { get; set; } = new();

        [JsonPropertyName("locations")]
        public List<string> Locations { get; set; } = new();
    }

    public class StaffUserUpdateDTO
    {
        [MaxLength(255), EmailAddress]
        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [MaxLength(100)]
        [JsonPropertyName("first_name")]
        public string? First_name { get; set; }

        [MaxLength(100)]
        [JsonPropertyName("last_name")]
        public string? Last_name { get; set; }

        // allow moving between Staff/Admin only
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UserRoles? Role { get; set; }
        
        [JsonPropertyName("location")]
        public string? Location { get; set; }
    }

    public class CreateStaffDTO
    {
        [MaxLength(255), EmailAddress]
        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [MaxLength(100)]
        [JsonPropertyName("first_name")]
        public string? First_name { get; set; }
        [JsonPropertyName("password")]
        public string? Password { get; set; }

        [MaxLength(100)]
        [JsonPropertyName("last_name")]
        public string? Last_name { get; set; }

        // allow moving between Staff/Admin only
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UserRoles? Role { get; set; }

        [JsonPropertyName("status")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public UserStatus Status { get; set; }

        [JsonPropertyName("location_id")]
        public int? Location_Id { get; set; }

        // Locations staff may work at; location_id, if given, is the one they start at
        [JsonPropertyName("location_ids")]
        public List<int>? Location_Ids { get; set; }
    }

    public class StaffMemberChangePasswordDTO
    {
        [Required, MinLength(8), MaxLength(200)]
        [JsonPropertyName("password")]
        public string New_password { get; set; } = string.Empty;

        [Required, MinLength(8), MaxLength(200)]
        [JsonPropertyName("current_password")]
        public string Current_password { get; set; } = string.Empty;
    }
    
    public class StaffMemberUpdateDTO
    {
                [JsonPropertyName("created_at")]
        public DateTime Created_At { get; set; }

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
        public int User_Id { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        // Locations staff may work at. Null leaves them unchanged
        [JsonPropertyName("location_ids")]
        public List<int>? Location_Ids { get; set; }
    }

    // The signed-in user's locations, for the app bar's location picker
    public class MyLocationsDTO
    {
        // The location they're working at now; null when they have none
        [JsonPropertyName("current_location")]
        public LocationOptionDTO? Current_Location { get; set; }

        // Staff: the locations an admin assigned them. Admins: every location
        [JsonPropertyName("locations")]
        public List<LocationOptionDTO> Locations { get; set; } = new();

        // Admins can work at any location
        [JsonPropertyName("all_locations")]
        public bool All_Locations { get; set; }
    }

    public class LocationOptionDTO
    {
        [JsonPropertyName("location_id")]
        public int Location_Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}