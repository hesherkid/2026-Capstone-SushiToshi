using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using back_end.domain.enums;

namespace back_end.domain.Entities
{
    [Table("menu_item_assignment")]
    public class MenuItemAssignment
    {
        //Composite Key
        //Foreign Key to Menu ID

        [Column("menu_id")]
        [ForeignKey(nameof(Menu))]
        public int Menu_Id { get; set; }

        [Column("item_id")]
        [ForeignKey(nameof(MenuItem))]
        public int Item_Id { get; set; }

        public decimal Price { get; set; }

        public int Total_Units_Ordered { get; set; }

        public DateTime LastOrdered { get; set; }

        public bool Is_Add_On { get; set; }

        public MenuItemStatus Status { get; set; }

        public int Adult_Limit { get; set; }

        public int Child_limit { get; set; }

        public int Senior_limit { get; set; }

        public int Tot_Limit { get; set; }

        public Menu Menu { get; set; } = null!;

        public Menu_Item MenuItem { get; set; } = null!;
    }

}