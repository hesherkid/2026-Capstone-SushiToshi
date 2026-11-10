using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using back_end.domain;
using back_end.domain.Entities;
using back_end.domain.enums;

namespace back_end.Dtos
{
    public class MenuItemAssignmentBaseDto
    {
        public int MenuId { get; set; }
        public int ItemId { get; set; }
        [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative")]
        public decimal Price { get; set; }
        public OverLimitBehavior? OverLimitBehavior { get; set; }
        public int? TotLimit { get; set; } = 0; // optional: 0 = same as child
        public int? PerOrderLimit { get; set; } = 0;
        public int? LocationId { get; set; }   // null = default for all locations
    }

    public class MenuAssignmentCreate : MenuItemAssignmentBaseDto
    {
        [Required(ErrorMessage = "Adult limit is required (0 = unlimited)")]
        public int? AdultLimit { get; set; }
        [Required(ErrorMessage = "Child limit is required (0 = unlimited)")]
        public int? ChildLimit { get; set; }
        [Required(ErrorMessage = "Senior limit is required (0 = unlimited)")]
        public int? SeniorLimit { get; set; }
        public MenuItemStatus Status { get; set; } = MenuItemStatus.Available;
        public bool IsAddOn { get; set; } = false;
    }

    public class MenuAssignmentUpdateDto
    {
        [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative")]
        public decimal? Price { get; set; }
        public OverLimitBehavior? OverLimitBehavior { get; set; }
        public int? AdultLimit { get; set; }
        public int? ChildLimit { get; set; }
        public int? SeniorLimit { get; set; }
        public int? TotLimit { get; set; }
        public MenuItemStatus? Status { get; set; } // no default, so "not sent" stays null
        public bool? IsAddOn { get; set; }
        public int? PerOrderLimit { get; set; }
    }

    // MenuName is null unless the assignment's Menu is loaded
    public class MenuAssignmentResponseDto
    {
        public int AssignmentId { get; set; }
        public int MenuId { get; set; }
        public string? MenuName { get; set; }
        public int ItemId { get; set; }
        public int? LocationId { get; set; }   // null = default for all locations
        public decimal Price { get; set; }
        public bool IsAddOn { get; set; }
        public MenuItemStatus Status { get; set; }
        public int AdultLimit { get; set; }
        public int ChildLimit { get; set; }
        public int SeniorLimit { get; set; }
        public int TotLimit { get; set; }
        public int PerOrderLimit { get; set; }
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