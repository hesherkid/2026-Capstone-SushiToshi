

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using back_end.domain;
using back_end.domain.enums;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace back_end.DTO.MenuDTO{
    public class MenuBaseDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        [JsonPropertyName("start_time")]
        public TimeOnly? Start_Time { get; set; }
        [JsonPropertyName("end_time")]
        public TimeOnly? End_Time { get; set; }
        [JsonPropertyName("is_active")]
        public bool Is_Active { get; set; } = true;

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
        public int Menu_Id { get; set; }
    }
    
    public class MenuItemDetailResponseDto
    {
        [JsonPropertyName("menu_id")]
        public int Menu_Id { get; set; } 
        [JsonPropertyName("item_id")]
        public int Item_Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("description")]
        public string? Description { get; set; } 
        [JsonPropertyName("category_id")]
        public int Category_Id { get; set; }
        [JsonPropertyName("category_name")]
        public string Category_Name { get; set; } = string.Empty;
        [JsonPropertyName("price")]
        public decimal Price { get; set; }
        [JsonPropertyName("is_add_on")]
        public bool Is_Add_On { get; set; }
        [JsonPropertyName("status")]
        public MenuItemStatus Status { get; set; } = MenuItemStatus.Available;
        [JsonPropertyName("item_image_url")]
        public string? Item_Image_Url { get; set; } 

    }
}