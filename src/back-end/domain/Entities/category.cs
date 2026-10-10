using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        [MaxLength(255)]
        public int total_views { get; set; } = 0;

        public int total_view_seconds { get; set; } = 0;

        public DateTime last_viewed_at { get; set; }

        public int adult_limit { get; set; } = 0;

        public int child_limit { get; set; } = 0;

        public int senior_limit { get; set; } = 0;

        public int total_limit { get; set; } = 0; // TODO: is this a typo? should it be tot_limit or is there a table limit?

        public ICollection<Menu_Item> MenuItems { get; set; } = new List<Menu_Item>();
    }
}