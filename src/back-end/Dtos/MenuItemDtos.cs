

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using back_end.domain;
using back_end.domain.Entities;
using back_end.domain.enums;

namespace back_end.Dtos
{
    public class MenuItemBaseDto
    {
        [MinLength(1)]
        [MaxLength(255)]
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        [JsonPropertyName("category_id")]
        public int CategoryId { get; set; }
        [JsonPropertyName("item_image_url")]
        public string? ItemImageUrl { get; set; }
        //public bool is_add_on {get;set;}
        [JsonPropertyName("status")]
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
        [JsonPropertyName("tag_ids")]
        public List<int> TagIds { get; set; } = [];
    }

    public class TagResponseDto
    {
        [JsonPropertyName("tag_id")]
        public int TagId { get; set; }
        [JsonPropertyName("name")]
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

        // Needs the item's MenuItemTags and their Tag loaded
        public static MenuItemResponseDto From(Menu_Item item) => Fill(new MenuItemResponseDto(), item);

        protected static T Fill<T>(T dto, Menu_Item item) where T : MenuItemResponseDto
        {
            dto.ItemId = item.item_id;
            dto.Name = item.Name;
            dto.Description = item.Description;
            dto.CategoryId = item.Category_id;
            dto.ItemImageUrl = item.image_url;
            dto.Status = item.Status;
            dto.Tags = item.MenuItemTags.Select(mit => new FullTagResponseDto
            {
                TagId = mit.Tag.tag_id,
                Name = mit.Tag.tag_name,
                ColorCode = mit.Tag.tag_color
            }).ToList();
            return dto;
        }
    }

    // GET /api/MenuItem/{id}: the item, plus every menu and location it's on
    public class MenuItemWithAssignmentsDto : MenuItemResponseDto
    {
        [JsonPropertyName("menu_assignments")]
        public List<MenuAssignmentResponseDto> MenuAssignments { get; set; } = [];

        // Also needs the item's MenuAssignments and their Menu loaded
        public static new MenuItemWithAssignmentsDto From(Menu_Item item)
        {
            var dto = Fill(new MenuItemWithAssignmentsDto(), item);
            dto.MenuAssignments = item.MenuAssignments.Select(MenuAssignmentResponseDto.From).ToList();
            return dto;
        }
    }
    public class MenuItemUpdateDto{
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        [JsonPropertyName("category_id")]
        public int? CategoryId { get; set; }
        [JsonPropertyName("item_image_url")]
        public string? ItemImageUrl { get; set; }
        [JsonPropertyName("status")]
        public MenuItemStatus? Status { get; set; }
        [JsonPropertyName("tag_ids")]
        public List<int>? TagIds { get; set; }  
    }
    
}