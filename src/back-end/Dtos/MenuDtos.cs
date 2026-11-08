

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using back_end.domain;
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
        public string? Name { get; set; }
        public string? Description { get; set; }
        public TimeOnly? Start_Time { get; set; }
        public TimeOnly? End_Time { get; set; }
        public bool? Is_Active { get; set; }
    }

    public class MenuResponseDto : MenuBaseDto
    {
        [JsonPropertyName("menu_id")]
        public int MenuId { get; set; }
    }
    
    public class MenuItemDetailResponseDto
    {
        [JsonPropertyName("menu_id")]
        public int MenuId { get; set; } 
        [JsonPropertyName("item_id")]
        public int ItemId { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("description")]
        public string? Description { get; set; } 
        [JsonPropertyName("category_id")]
        public int CategoryId { get; set; }
        [JsonPropertyName("category_name")]
        public string CategoryName { get; set; } = string.Empty;
        [JsonPropertyName("price")]
        public decimal Price { get; set; }
        [JsonPropertyName("is_add_on")]
        public bool IsAddOn { get; set; }
        [JsonPropertyName("status")]
        public MenuItemStatus Status { get; set; } = MenuItemStatus.Available;
        [JsonPropertyName("item_image_url")]
        public string? ItemImageUrl { get; set; } 

    }
}