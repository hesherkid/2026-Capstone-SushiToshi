using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using back_end.domain.enums;

namespace back_end.domain.Entities
{
    [Table("menu_item")]
    public class Menu_Item
    {
        [Key]
        public int item_id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [ForeignKey(nameof(Category))]
        public int Category_id { get; set; }
        public string? image_url { get; set; }

        public MenuItemStatus Status { get; set; } = MenuItemStatus.Available;


        public Category Category { get; set; } = null!;
        public ICollection<MenuItemAssignment> MenuAssignments { get; set; } = new List<MenuItemAssignment>();
        public ICollection<MenuItemTag> MenuItemTags { get; set; } = new List<MenuItemTag>();
        public ICollection<OrderItems> OrderItems { get; set; } = new List<OrderItems>();

    }
}