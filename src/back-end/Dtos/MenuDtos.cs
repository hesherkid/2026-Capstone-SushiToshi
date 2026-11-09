

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using back_end.domain;
using back_end.domain.Entities;
using back_end.domain.enums;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace back_end.Dtos{
    public class MenuBaseDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        [JsonPropertyName("start_time")]
        public TimeOnly? StartTime { get; set; }
        [JsonPropertyName("end_time")]
        public TimeOnly? EndTime { get; set; }
        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; } = true;

    }

    public class MenuCreateDto : MenuBaseDto
    {

    }
    public class MenuUpdateDto
    {
        [MinLength(1)]
        [MaxLength(255)]
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        [JsonPropertyName("start_time")]
        public TimeOnly? StartTime { get; set; }
        [JsonPropertyName("end_time")]
        public TimeOnly? EndTime { get; set; }
        [JsonPropertyName("is_active")]
        public bool? IsActive { get; set; }
    }

    public class MenuResponseDto : MenuBaseDto
    {
        [JsonPropertyName("menu_id")]
        public int MenuId { get; set; }
        [JsonPropertyName("location_ids")]
        public List<int> LocationIds { get; set; } = [];

        // LocationIds is empty unless the menu's MenuLocations are loaded
        public static MenuResponseDto From(Menu menu) => new()
        {
            MenuId = menu.Menu_id,
            Name = menu.Name,
            Description = menu.Description,
            StartTime = menu.Start_time,
            EndTime = menu.End_time,
            IsActive = menu.Is_active,
            LocationIds = menu.MenuLocations.Select(ml => ml.Location_Id).ToList(),
        };
    }
    
}