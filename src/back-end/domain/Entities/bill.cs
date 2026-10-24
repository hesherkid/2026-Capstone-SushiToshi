using back_end.domain.enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace back_end.domain.Entities
{
    [Table("bills")]
    public class Billing
    {
        [Key]
        public int Bill_Id { get; set; }

        [ForeignKey(nameof(DiningSession))]
        public int Session_Id { get; set; }

        [MaxLength(255)]
        public string Bill_Name { get; set; } = string.Empty;

        public int Senior_Count { get; set; }

        public int Adult_Count { get; set; }

        public int Child_Count { get; set; }

        public int Tot_Count { get; set; } // toddler

        public int Total_Count { get; set; }

        public BillStatus Status { get; set; } = BillStatus.Open;

        public DateTime Created_At { get; set; } = DateTime.UtcNow;

        public DateTime? Closed_At { get; set; }

        // When the guests pressed "Ready for Bill". After that no more orders can go on this bill.
        public DateTime? Requested_At { get; set; }

        public decimal Leftover_Charge { get; set; } = 0m;

        // Prices frozen when the bill closes, so later price changes don't alter old bills.
        // NULL = not closed yet (or closed before snapshots existed): prices are worked out live.
        public PricingDayType? Pricing_Type { get; set; }

        [MaxLength(100)]
        public string? Holiday_Name { get; set; }

        [Precision(10, 2)] public decimal? Adult_Price { get; set; }
        [Precision(10, 2)] public decimal? Senior_Price { get; set; }
        [Precision(10, 2)] public decimal? Child_Price { get; set; }
        [Precision(10, 2)] public decimal? Toddler_Price { get; set; }
        [Precision(5, 4)] public decimal? Tax_Rate { get; set; }
        // The gratuity rate actually charged (0 if the party was too small when the bill closed),
        // so cancelling another bill at the table later can't change this one
        [Precision(5, 4)] public decimal? Gratuity_Rate { get; set; }
        public int? Gratuity_Min_Party_Size { get; set; }

        public DiningSession DiningSession { get; set; } = null!;
        public ICollection<SessionOrder> Orders { get; set; } = new List<SessionOrder>();


    }

}