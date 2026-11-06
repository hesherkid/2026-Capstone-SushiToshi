using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace back_end.domain.Entities
{
    [Table("menu")]
    public class Menu
    {
        [Key]
        public int Menu_id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public TimeOnly Start_time { get; set; }

        public TimeOnly End_time { get; set; }

        public bool Is_active { get; set; } = true;


        public ICollection<MenuItemAssignment> MenuItemAssignments { get; set; } = new List<MenuItemAssignment>();
        public ICollection<DiningSession> DiningSessions { get; set; } = new List<DiningSession>();
        public ICollection<OrderItems> OrderItems { get; set; } = new List<OrderItems>();
        public ICollection<MenuLocations> MenuLocations { get; set; } = new List<MenuLocations>();
        
    }
}