using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using back_end.domain.enums;

namespace back_end.domain.Entities
{
    [Table("session_order")]
    public class SessionOrder
    {
        [Key]
        public int Order_Id { get; set; }

        [ForeignKey(nameof(DiningSession))]
        public int session_id { get; set; }

        [ForeignKey(nameof(Bill))]
        public int Bill_Id { get; set; }

        // User information - nullable to support guest orders
        [ForeignKey(nameof(User))]
        public int? User_Id { get; set; }
        [MaxLength(200)]
        public string? User_Name { get; set; }

        public OrderStatus Status { get; set; }

        public DateTime Created_At { get; set; }

        public DateTime? Completed_At { get; set; }

        public DateTime? Approved_At { get; set; }
        public int? Approved_By { get; set; }

        public DiningSession DiningSession { get; set; } = null!;
        public Billing Bill { get; set; } = null!;
        public User? User { get; set; }
        public ICollection<OrderItems> OrderItems { get; set; } = new List<OrderItems>();

    }

}
