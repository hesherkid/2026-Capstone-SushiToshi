using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace back_end.domain.Entities
{
    [Table("tag")]
    public class Tag
    {
        [Key]
        public int tag_id { get; set; }

        [MaxLength(100)]
        public string tag_name { get; set; } = string.Empty;
        [MaxLength(7)]
        public string tag_color { get; set; } = string.Empty;


        public ICollection<MenuItemTag> MenuItemTags { get; set; } = new List<MenuItemTag>();
    }

}