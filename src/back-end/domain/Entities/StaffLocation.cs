using System.ComponentModel.DataAnnotations.Schema;
namespace back_end.domain.Entities
{
    // A location an admin has assigned a staff member to. The location they're
    // working at right now is User.Location_id, which must be one of these
    [Table("staff_location")]
    public class StaffLocation
    {
        [ForeignKey(nameof(User))]
        public int User_Id { get; set; }

        [ForeignKey(nameof(Location))]
        public int Location_Id { get; set; }

        public User User { get; set; } = null!;
        public Locations Location { get; set; } = null!;
    }

}
