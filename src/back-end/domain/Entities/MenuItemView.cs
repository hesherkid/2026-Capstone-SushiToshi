using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace back_end.domain.Entities
{
    [Table("menu_item_view")]
    public class MenuItemView
    {
        [Key]
        public long View_Id { get; set; }

        public int Item_Id { get; set; }

        public int Session_Id { get; set; }

        public int Menu_Id { get; set; }

        public int Location_Id { get; set; }

        public int? User_Id { get; set; }

        // <summary> When the view started, UTC, from ther server clock </summary>
        public DateTime Viewed_At { get; set; }

        /// <summary>Seconds the item was on screen, capped at ViewTrackingRules.MaxRecordedSeconds.</summary>
        public int View_Seconds { get; set; }

        /// <summary>False when the item or its menu assignment was Unavailable at the time of the view.</summary>
        public bool Was_Available { get; set; }

        public Menu_Item MenuItem { get; set; } = null!;

        public DiningSession DiningSession { get; set; } = null!;
    }
}