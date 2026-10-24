using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using back_end.domain.enums;

namespace back_end.domain.Entities;

// One row per day type (Weekday / Weekend / Holiday) for each profile
[Table("pricing_rates")]
public class PricingRate
{
    [Key]
    public int Pricing_Rate_Id { get; set; }

    [ForeignKey(nameof(PricingProfile))]
    public int Pricing_Profile_Id { get; set; }

    public PricingDayType Day_Type { get; set; }

    [Precision(10, 2)]
    public decimal Adult_Price { get; set; }

    [Precision(10, 2)]
    public decimal Senior_Price { get; set; }

    [Precision(10, 2)]
    public decimal Child_Price { get; set; }

    [Precision(10, 2)]
    public decimal Toddler_Price { get; set; }

    public PricingProfile PricingProfile { get; set; } = null!;
}
