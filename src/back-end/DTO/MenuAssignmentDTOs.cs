using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using back_end.domain;
using back_end.domain.enums;
using Microsoft.AspNetCore.SignalR;

namespace back_end.DTO.MenuItemAssignmentDTO
{
    public class MenuItemAssignmentBaseDTO
    {
        public int Menu_Id { get; set; }
        public int Item_Id { get; set; }
        [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative")]
        public decimal Price { get; set; }
        [Range(0, double.MaxValue, ErrorMessage = "Full price cannot be negative")]
        public decimal? Full_Price { get; set; }
        public OverLimitBehavior? Over_Limit_Behavior { get; set; }
        public int? Tot_Limit { get; set; } = 0; // optional: 0 = same as child
        public int? Location_Id { get; set; }   // null = default for all locations
    }

    public class MenuAssignmentCreate : MenuItemAssignmentBaseDTO
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

    public class MenuAssignmentUpdateDTO
    {
        [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative")]
        public decimal? Price { get; set; }
        [Range(0, double.MaxValue, ErrorMessage = "Full price cannot be negative")]
        public decimal? Full_Price { get; set; }
        public OverLimitBehavior? Over_Limit_Behavior { get; set; }
        public int? Adult_Limit { get; set; }
        public int? Child_Limit { get; set; }
        public int? Senior_Limit { get; set; }
        public int? Tot_Limit { get; set; }
        public MenuItemStatus? Status { get; set; } // no default, so "not sent" stays null
        public bool? Is_Add_on { get; set; }
    }

    class MenuAssignmentResponse : MenuItemAssignmentBaseDTO
    {
        public bool? Is_Add_On { get; set; }
        public MenuItemStatus Status { get; set; } = MenuItemStatus.Available;
    }

}