

using System.ComponentModel.DataAnnotations;
using back_end.domain;
using back_end.domain.Entities;
using back_end.domain.enums;

namespace back_end.Dtos
{
    public class MenuItemBaseDto
    {
        [MinLength(1)]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int CategoryId { get; set; }
        public string? ItemImageUrl { get; set; }
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
        public List<int> TagIds { get; set; } = [];
    }

    public class TagResponseDto
    {
        public int TagId { get; set; }
        public string Name { get; set; } = string.Empty;

    }
    public class FullTagResponseDto : TagResponseDto
    {
        public string ColorCode { get; set; } = string.Empty;

    }

    public class MenuItemResponseDto : MenuItemBaseDto
    {
        public int ItemId { get; set; }
        public List<FullTagResponseDto> Tags { get; set; } = [];
        public DateTime? CreatedAt { get; set; }
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
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int? CategoryId { get; set; }
        public string? ItemImageUrl { get; set; }
        public MenuItemStatus? Status { get; set; }
        public List<int>? TagIds { get; set; }  
    }
    
}