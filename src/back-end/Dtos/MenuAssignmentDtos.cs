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
        [JsonPropertyName("menu_id")]
        public int MenuId { get; set; }
        [JsonPropertyName("item_id")]
        public int ItemId { get; set; }
        [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative")]
        [JsonPropertyName("price")]
        public decimal Price { get; set; }
        [JsonPropertyName("over_limit_behavior")]
        public OverLimitBehavior? OverLimitBehavior { get; set; }
        [JsonPropertyName("tot_limit")]
        public int? TotLimit { get; set; } = 0; // optional: 0 = same as child
        [JsonPropertyName("per_order_limit")]
        public int? PerOrderLimit { get; set; } = 0;
        [JsonPropertyName("location_id")]
        public int? LocationId { get; set; }   // null = default for all locations
    }

    public class MenuAssignmentCreate : MenuItemAssignmentBaseDto
    {
        [Required(ErrorMessage = "Adult limit is required (0 = unlimited)")]
        [JsonPropertyName("adult_limit")]
        public int? AdultLimit { get; set; }
        [Required(ErrorMessage = "Child limit is required (0 = unlimited)")]
        [JsonPropertyName("child_limit")]
        public int? ChildLimit { get; set; }
        [Required(ErrorMessage = "Senior limit is required (0 = unlimited)")]
        [JsonPropertyName("senior_limit")]
        public int? SeniorLimit { get; set; }
        [JsonPropertyName("status")]
        public MenuItemStatus Status { get; set; } = MenuItemStatus.Available;
        [JsonPropertyName("is_add_on")]
        public bool IsAddOn { get; set; } = false;
    }

    public class MenuAssignmentUpdateDto
    {
        [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative")]
        [JsonPropertyName("price")]
        public decimal? Price { get; set; }
        [JsonPropertyName("over_limit_behavior")]
        public OverLimitBehavior? OverLimitBehavior { get; set; }
        [JsonPropertyName("adult_limit")]
        public int? AdultLimit { get; set; }
        [JsonPropertyName("child_limit")]
        public int? ChildLimit { get; set; }
        [JsonPropertyName("senior_limit")]
        public int? SeniorLimit { get; set; }
        [JsonPropertyName("tot_limit")]
        public int? TotLimit { get; set; }
        [JsonPropertyName("status")]
        public MenuItemStatus? Status { get; set; } // no default, so "not sent" stays null
        [JsonPropertyName("is_add_on")]
        public bool? IsAddOn { get; set; }
        [JsonPropertyName("per_order_limit")]
        public int? PerOrderLimit { get; set; }
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