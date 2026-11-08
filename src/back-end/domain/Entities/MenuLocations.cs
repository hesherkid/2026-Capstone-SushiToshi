using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace back_end.domain.Entities
{
    [Table("menu_location")]
    public class MenuLocations
    {
        [ForeignKey(nameof(Menu))]
        public int Menu_Id { get; set; }
        
        [ForeignKey(nameof(Location))]
        public int Location_Id { get; set; }

        public Menu Menu { get; set; } = null!;
        public Locations Location { get; set; } = null!;
    }

}