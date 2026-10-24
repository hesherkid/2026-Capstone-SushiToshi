using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace back_end.domain.Entities;

[Table("pricing_profiles")]
public class PricingProfile
{
    [Key]
    public int Pricing_Profile_Id { get; set; }

    // NULL = the default profile for all locations; a value = this location's own pricing
    [Column("location_id")]
    [ForeignKey(nameof(Location))]
    public int? Location_Id { get; set; }

    // Database-computed IFNULL(location_id, 0); used only by the unique index
    [Column("location_key")]
    public int Location_Key { get; private set; }

    // GST only in Alberta (0.05)
    [Precision(5, 4)]
    public decimal Tax_Rate { get; set; }

    // Automatic gratuity once the table has this many guests; 0 = never
    [Precision(5, 4)]
    public decimal Gratuity_Rate { get; set; }

    public int Gratuity_Min_Party_Size { get; set; }

    [Precision(10, 2)]
    public decimal Leftover_Fee { get; set; }

    // Days that get Weekend prices, stored as "Friday,Saturday,Sunday"
    [MaxLength(100)]
    public List<DayOfWeek> Weekend_Days { get; set; } = new();

    // Lets a location ignore holiday pricing even when its province has holidays
    public bool Apply_Holiday_Pricing { get; set; } = true;

    public DateTime Created_At { get; set; } = DateTime.UtcNow;

    public DateTime Updated_At { get; set; } = DateTime.UtcNow;

    public Locations? Location { get; set; }
    public ICollection<PricingRate> Rates { get; set; } = new List<PricingRate>();
}
