using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using back_end.domain;
using back_end.domain.Entities;
using Microsoft.AspNetCore.SignalR;

namespace back_end.Dtos
{
    public class LocationBaseDto
    {
        [MaxLength(125)]
        [MinLength(1)]
        public string Name { get; set; } = string.Empty;
        [MaxLength(75)]
        [MinLength(1)]
        [JsonPropertyName("address_one")]
        public string AddressOne { get; set; } = string.Empty;
        [MaxLength(75)]
        [JsonPropertyName("address_two")]
        public string AddressTwo { get; set; } = string.Empty;
        [MaxLength(50)]
        [MinLength(1)]
        public string City { get; set; } = string.Empty;
        [MaxLength(20)]
        [MinLength(1)]
        public string Province { get; set; } = string.Empty;
        // Two-letter code for holidays, e.g. "AB". Optional: Alberta on create, unchanged on update
        [RegularExpression("^(AB|BC|MB|NB|NL|NS|NT|NU|ON|PE|QC|SK|YT)$")]
        [JsonPropertyName("province_code")]
        public string? ProvinceCode { get; set; }
        [MaxLength(8)]
        [MinLength(1)]
        [JsonPropertyName("postal_code")]
        public string PostalCode { get; set; } = string.Empty;
        [MaxLength(11)]
        [MinLength(10)]
        [JsonPropertyName("phone_number")]
        public string PhoneNumber { get; set; } = string.Empty;

    }

    public class LocationCreateDto : LocationBaseDto
    {

    }

    public class LocationUpdateDto : LocationBaseDto { }

    public class LocationResponseDto : LocationBaseDto
    {
        [JsonPropertyName("location_id")]
        public int LocationId { get; set; }

        public static LocationResponseDto From(Locations location) => new()
        {
            LocationId = location.Location_Id,
            Name = location.Name,
            AddressOne = location.Address_Primary,
            AddressTwo = location.Address_Secondary ?? string.Empty,
            City = location.City,
            Province = location.Province,
            ProvinceCode = location.Province_Code,
            PostalCode = location.Postal_Code,
            PhoneNumber = location.Phone_Number,
        };
    }

    public class MenuLocationCreateDto
    {
        [Required]
        [JsonPropertyName("menu_id")]
        public int MenuId { get; set; }
    }

}