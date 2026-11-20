using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using back_end.domain.enums;

namespace back_end.domain.Entities
{
    [Table("Categories")]
    public class Category
    {
        [Key]

        public int Category_id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Category_name { get; set; } = string.Empty;

        [Required]

        public string Description { get; set; } = string.Empty;

        public string? image_url { get; set; }

        public int adult_limit { get; set; } = 0;

        public int child_limit { get; set; } = 0;

        public int senior_limit { get; set; } = 0;

        public int toddler_limit { get; set; } = 0;

        public int per_order_limit { get; set; } = 0;

        // True = per order limit is for each guest at the table, one order can have
        //  per order limit * everyone on the table's bills (Sashimi: 30 per guest = 120 for a 4-top)
        public bool per_order_per_guest { get; set; } = false;

        public OverLimitBehavior over_limit_behavior { get; set; } = OverLimitBehavior.Block;

        // The free ice cream (or dessert) staff send when guests ask for the bill: its Available items
        // are the flavours staff choose from. They can't go on a menu, so guests can't order them
        public bool Free_With_Bill { get; set; }

        public ICollection<Menu_Item> MenuItems { get; set; } = new List<Menu_Item>();
    }
}