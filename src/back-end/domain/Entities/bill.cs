using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using back_end.domain.enums;

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

        public DiningSession DiningSession { get; set; } = null!;
        public ICollection<SessionOrder> Orders { get; set; } = new List<SessionOrder>();


    }

}