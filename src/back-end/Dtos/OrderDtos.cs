using System.ComponentModel.DataAnnotations;
using back_end.domain.enums;

namespace back_end.Dtos
{
    public class OrderItemCreateDto
    {
        public int OrderId { get; set; }
        public int MenuId { get; set; }
        public int ItemId { get; set; }

        public int Quantity { get; set; }

        public decimal PriceAtTime { get; set; }

        public OrderStatus Status { get; set; }

    }

    public class OrderItemResponseDto
    {
        public int OrderItemId { get; set; }

        public int OrderId { get; set; }

        public int MenuId { get; set; }

        public int ItemId { get; set; }

        public int Quantity { get; set; }

        public decimal PriceAtTime { get; set; }

        public OrderStatus Status { get; set; }
        public DateTime? CompletedAt { get; set; }

        public string? Name { get; set; }

        // "OverLimit" for extra units charged past a limit
        public string? ChargeReason { get; set; }

    }
    public class OrderItemInsertResponse
    {
        public int OrderItemId { get; set; }

        public int OrderId { get; set; }

        public int MenuId { get; set; }

        public int ItemId { get; set; }

        public int Quantity { get; set; }

        public decimal PriceAtTime { get; set; }

        public OrderStatus Status { get; set; }

    }
    /// <summary>
    /// How much the table can still order before a limit that blocks stops them.
    /// null = no blocking limit. Charge limits don't block: going over them is allowed at full price,
    /// so they're reported separately as Free_Left.
    /// </summary>
    public class OrderLimitLeftDto
    {
        public int? PerOrder { get; set; }

        public int? SessionLeft { get; set; }

        /// <summary>
        /// Units left before a Charge limit bills each extra; null = no Charge limit
        /// </summary>
        public int? FreeLeft { get; set; }
    }

    public class ItemOrderLimitDto : OrderLimitLeftDto
    {
        public int ItemId { get; set; }

        public int CategoryId { get; set; }

        /// <summary>
        /// What each unit past Free_Left (the item's or its category's) costs
        /// </summary>
        public decimal ExtraPrice { get; set; }
    }

    public class CategoryOrderLimitDto : OrderLimitLeftDto
    {
        public int CategoryId { get; set; }
    }

    public class SessionOrderLimitsDto
    {
        public List<ItemOrderLimitDto> Items { get; set; } = new();

        public List<CategoryOrderLimitDto> Categories { get; set; } = new();
    }

    public class OrderItemUpdateDto
    {
        public int? Quantity { get; set; }
        public decimal? PriceAtTime { get; set; }
        public string? Status { get; set; }
    }
    public class OrderCreateDto
    {
        public int SessionId { get; set; }

        public int BillId { get; set; }

        // Saved together with the order, so an order is never left without items
        public List<OrderItemCreateDto> Items { get; set; } = new();
    }

    // public class OrderUpdateDto
    // {
    //     [JsonPropertyName("status")]
    //     public OrderStatus Status { get; set; }
    // }

    public class OrderResponseDto
    {
        public int OrderId { get; set; }
        public int SessionId { get; set; }
        public int BillId { get; set; }
        public int? UserId { get; set; }
        public string? UserName { get; set; }
        public OrderStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public List<OrderItemResponseDto> OrderItems { get; set; } = new();

        // Automatically calculated from OrderItems
        public int PendingItemsCount
        {
            get
            {
                if (OrderItems == null)
                    return 0;

                return OrderItems.Count(item => item.Status == OrderStatus.Pending);
            }
        }
    }
    
    // public class OrderStatusResponseDto
    // {
    //     [JsonPropertyName("order_id")]
    //     public int Order_Id { get; set; }

    //     [JsonPropertyName("status")]
    //     public OrderStatus Status { get; set; }

    //     [JsonPropertyName("bill_id")]
    //     public int Bill_Id { get; set; }
    // }
}