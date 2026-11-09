using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using back_end.domain;
using back_end.domain.Entities;
using back_end.domain.enums;

namespace back_end.Dtos
{
    public class MenuItemAssignmentBaseDto
    {
        public int Menu_Id { get; set; }
        public int Item_Id { get; set; }
        [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative")]
        public decimal Price { get; set; }
        [Range(0, double.MaxValue, ErrorMessage = "Full price cannot be negative")]
        public OverLimitBehavior? Over_Limit_Behavior { get; set; }
        public int? Tot_Limit { get; set; } = 0; // optional: 0 = same as child
        public int? Per_Order_Limit { get; set; } = 0;
        public int? Location_Id { get; set; }   // null = default for all locations
    }

    public class MenuAssignmentCreate : MenuItemAssignmentBaseDto
    {
        [Required(ErrorMessage = "Adult limit is required (0 = unlimited)")]
        public int? Adult_Limit { get; set; }
        [Required(ErrorMessage = "Child limit is required (0 = unlimited)")]
        public int? Child_Limit { get; set; }
        [Required(ErrorMessage = "Senior limit is required (0 = unlimited)")]
        public int? Senior_Limit { get; set; }
        public MenuItemStatus Status { get; set; } = MenuItemStatus.Available;
        public bool Is_Add_on { get; set; } = false;
    }

    public class MenuAssignmentUpdateDto
    {
        [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative")]
        public decimal? Price { get; set; }
        [Range(0, double.MaxValue, ErrorMessage = "Full price cannot be negative")]
        public OverLimitBehavior? Over_Limit_Behavior { get; set; }
        public int? Adult_Limit { get; set; }
        public int? Child_Limit { get; set; }
        public int? Senior_Limit { get; set; }
        public int? Tot_Limit { get; set; }
        public MenuItemStatus? Status { get; set; } // no default, so "not sent" stays null
        public bool? Is_Add_on { get; set; }
        public int? Per_Order_Limit { get; set; }
    }

    // MenuName is null unless the assignment's Menu is loaded
    public class MenuAssignmentResponseDto
    {
        [JsonPropertyName("assignment_id")]
        public int AssignmentId { get; set; }
        [JsonPropertyName("menu_id")]
        public int MenuId { get; set; }
        [JsonPropertyName("menu_name")]
        public string? MenuName { get; set; }
        [JsonPropertyName("item_id")]
        public int ItemId { get; set; }
        [JsonPropertyName("location_id")]
        public int? LocationId { get; set; }   // null = default for all locations
        [JsonPropertyName("price")]
        public decimal Price { get; set; }
        [JsonPropertyName("is_add_on")]
        public bool IsAddOn { get; set; }
        [JsonPropertyName("status")]
        public MenuItemStatus Status { get; set; }
        [JsonPropertyName("adult_limit")]
        public int AdultLimit { get; set; }
        [JsonPropertyName("child_limit")]
        public int ChildLimit { get; set; }
        [JsonPropertyName("senior_limit")]
        public int SeniorLimit { get; set; }
        [JsonPropertyName("tot_limit")]
        public int TotLimit { get; set; }
        [JsonPropertyName("per_order_limit")]
        public int PerOrderLimit { get; set; }
        [JsonPropertyName("over_limit_behavior")]
        public OverLimitBehavior OverLimitBehavior { get; set; }

        public static MenuAssignmentResponseDto From(MenuItemAssignment a) => new()
        {
            AssignmentId = a.Assignment_Id,
            MenuId = a.Menu_Id,
            MenuName = a.Menu?.Name,
            ItemId = a.Item_Id,
            LocationId = a.Location_Id,
            Price = a.Price,
            IsAddOn = a.Is_Add_On,
            Status = a.Status,
            AdultLimit = a.Adult_Limit,
            ChildLimit = a.Child_limit,
            SeniorLimit = a.Senior_limit,
            TotLimit = a.Tot_Limit,
            PerOrderLimit = a.Per_Order_Limit,
            OverLimitBehavior = a.Over_Limit_Behavior,
        };
    }

}