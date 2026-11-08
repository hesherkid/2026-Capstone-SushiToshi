

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using back_end.domain;
using back_end.domain.enums;

namespace back_end.Dtos
{
    public class MenuItemBaseDto
    {
        [MinLength(1)]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Category_Id { get; set; }
        public string? Item_Image_Url { get; set; }
        //public bool is_add_on {get;set;}
        public MenuItemStatus Status { get; set; } = MenuItemStatus.Available;
    }

    // public class MenuItemResponseDto
    // {
    //     [JsonPropertyName("name")]
    //     public string? Name { get; set; }
    //     [JsonPropertyName("description")]
    //     public string? Description { get; set; }
    //     [JsonPropertyName("category_id")]
    //     public int Category_Id { get; set; }
    //     public string? Item_Image_Url { get; set; }
    //     [JsonPropertyName("status")]
    //     public MenuItemStatus? status { get; set; }

    //     public List<int>? Tag_Ids { get; set; }
    // }
    public class MenuItemCreateDto : MenuItemBaseDto
    {
        public List<int> Tag_Ids { get; set; } = [];
    }

    public class TagResponseDto
    {
        [JsonPropertyName("tag_id")]
        public int TagId { get; set; }
        public string Name { get; set; } = string.Empty;

    }
    public class FullTagResponseDto : TagResponseDto
    {
        [JsonPropertyName("color_code")]
        public string ColorCode { get; set; } = string.Empty;

    }

    public class MenuItemResponseDto : MenuItemBaseDto
    {
        [JsonPropertyName("item_id")]
        public int ItemId { get; set; }
        [JsonPropertyName("tags")]
        public List<FullTagResponseDto> Tags { get; set; } = [];
        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
    public class MenuItemDetailDto
    {
        public string Category_Name { get; set; } = string.Empty;
        public List<string> Tag_Names { get; set; } = [];
        public decimal? Current_Price { get; set; }
        public int Total_Orders { get; set; }

    }
    public class MenuItemUpdateDto{
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int? Category_Id { get; set; }
        public string? Item_Image_Url { get; set; }
        public MenuItemStatus? Status { get; set; }
        public List<int>? Tag_Ids { get; set; }  
    }
    
}