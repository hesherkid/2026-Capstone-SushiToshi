using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace back_end.domain.Entities
{
    [Table("menu_item_tag")]
    public class MenuItemTag
    {
        [Required]
        [ForeignKey(nameof(MenuItem))]
        public int Menu_item_id { get; set; }

        [Required]
        [ForeignKey(nameof(Tag))]
        public int Tag_id { get; set; }


        public Menu_Item MenuItem { get; set; } = null!;
        public Tag Tag { get; set; } = null!;
    }
}