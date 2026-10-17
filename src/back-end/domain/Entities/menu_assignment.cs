using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using back_end.domain.enums;

namespace back_end.domain.Entities
{
    [Table("menu_item_assignment")]
    public class MenuItemAssignment
    {
        [Key]
        [Column("assignment_id")]
        public int Assignment_Id { get; set; }          // new primary key (the old composite key can't include a nullable column)

        [Column("menu_id")]
        [ForeignKey(nameof(Menu))]
        public int Menu_Id { get; set; }

        [Column("item_id")]
        [ForeignKey(nameof(MenuItem))]
        public int Item_Id { get; set; }

        // NULL = default price for all locations; a value = override for that location
        [Column("location_id")]
        [ForeignKey(nameof(Location))]
        public int? Location_Id { get; set; }

        // Database-computed IFNULL(location_id, 0); used only by the unique index
        [Column("location_key")]
        public int Location_Key { get; private set; }


        public decimal Price { get; set; }

        public int Total_Units_Ordered { get; set; }

        public DateTime LastOrdered { get; set; }

        public bool Is_Add_On { get; set; }

        public MenuItemStatus Status { get; set; }

        public int Adult_Limit { get; set; }

        public int Child_limit { get; set; }

        public int Senior_limit { get; set; }

        public int Tot_Limit { get; set; }

        public OverLimitBehavior Over_Limit_Behavior { get; set; } = OverLimitBehavior.Block;

        public decimal Full_Price { get; set; } = 0m;

        public Menu Menu { get; set; } = null!;

        public Menu_Item MenuItem { get; set; } = null!;
        public Locations? Location { get; set; }

    }

}