using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using back_end.domain.enums;

namespace back_end.domain.Entities
{
    [Table("order_item")]
    public class OrderItems
    {
        [Key]
        public int Order_Item_Id { get; set; }

        [ForeignKey(nameof(SessionOrder))]
        public int Order_Key { get; set; }

        [ForeignKey(nameof(Menu))]
        public int Menu_Id { get; set; }

        [ForeignKey(nameof(MenuItem))]
        public int Item_Id { get; set; }

        public int Quantity { get; set; }

        public decimal Price_At_Time { get; set; }

        public OrderStatus Order_Item_Status { get; set; }

        public DateTime? Completed_At { get; set; }

        public ChargeReason Charge_Reason { get; set; } = ChargeReason.None;

        public SessionOrder SessionOrder { get; set; } = null!;
        public Menu Menu { get; set; } = null!;
        public Menu_Item MenuItem { get; set; } = null!;

    }

}
