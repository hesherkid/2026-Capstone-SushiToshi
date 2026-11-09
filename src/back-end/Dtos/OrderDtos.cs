using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using back_end.domain.enums;

namespace back_end.Dtos
{
    public class OrderItemCreateDto
    {
        [JsonPropertyName("order_id")]
        public int OrderId { get; set; }
        [JsonPropertyName("menu_id")]
        public int MenuId { get; set; }
        [JsonPropertyName("item_id")]
        public int ItemId { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("price_at_time")]
        public decimal PriceAtTime { get; set; }

        [JsonPropertyName("status")]
        public OrderStatus Status { get; set; }

    }

    public class OrderItemResponseDto
    {
        [JsonPropertyName("order_item_id")]
        public int OrderItemId { get; set; }

        [JsonPropertyName("order_id")]
        public int OrderId { get; set; }

        [JsonPropertyName("menu_id")]
        public int MenuId { get; set; }

        [JsonPropertyName("item_id")]
        public int ItemId { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("price_at_time")]
        public decimal PriceAtTime { get; set; }

        [JsonPropertyName("status")]
        public OrderStatus Status { get; set; }
        [JsonPropertyName("completed_at")]
        public DateTime? CompletedAt { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        // "OverLimit" for extra units charged past a limit
        [JsonPropertyName("charge_reason")]
        public string? ChargeReason { get; set; }

    }
    public class OrderItemInsertResponse
    {
        [JsonPropertyName("order_item_id")]
        public int OrderItemId { get; set; }

        [JsonPropertyName("order_id")]
        public int OrderId { get; set; }

        [JsonPropertyName("menu_id")]
        public int MenuId { get; set; }

        [JsonPropertyName("item_id")]
        public int ItemId { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("price_at_time")]
        public decimal PriceAtTime { get; set; }

        [JsonPropertyName("status")]
        public OrderStatus Status { get; set; }

    }
    /// <summary>
    /// How much the table can still order before a limit that blocks stops them.
    /// null = no blocking limit. Charge limits don't block: going over them is allowed at full price,
    /// so they're reported separately as Free_Left.
    /// </summary>
    public class OrderLimitLeftDto
    {
        [JsonPropertyName("per_order")]
        public int? PerOrder { get; set; }

        [JsonPropertyName("session_left")]
        public int? SessionLeft { get; set; }

        /// <summary>
        /// Units left before a Charge limit bills each extra; null = no Charge limit
        /// </summary>
        [JsonPropertyName("free_left")]
        public int? FreeLeft { get; set; }
    }

    public class ItemOrderLimitDto : OrderLimitLeftDto
    {
        [JsonPropertyName("item_id")]
        public int ItemId { get; set; }

        [JsonPropertyName("category_id")]
        public int CategoryId { get; set; }

        /// <summary>
        /// What each unit past Free_Left (the item's or its category's) costs
        /// </summary>
        [JsonPropertyName("extra_price")]
        public decimal ExtraPrice { get; set; }
    }

    public class CategoryOrderLimitDto : OrderLimitLeftDto
    {
        [JsonPropertyName("category_id")]
        public int CategoryId { get; set; }
    }

    public class SessionOrderLimitsDto
    {
        [JsonPropertyName("items")]
        public List<ItemOrderLimitDto> Items { get; set; } = new();

        [JsonPropertyName("categories")]
        public List<CategoryOrderLimitDto> Categories { get; set; } = new();
    }

    public class OrderItemUpdateDto
    {
        [JsonPropertyName("quantity")]
        public int? Quantity { get; set; }
        [JsonPropertyName("price_at_time")]
        public decimal? PriceAtTime { get; set; }
        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }
    public class OrderCreateDto
    {
        [JsonPropertyName("session_id")]
        public int SessionId { get; set; }

        [JsonPropertyName("bill_id")]
        public int BillId { get; set; }

        // Saved together with the order, so an order is never left without items
        [JsonPropertyName("items")]
        public List<OrderItemCreateDto> Items { get; set; } = new();
    }

    // public class OrderUpdateDto
    // {
    //     [JsonPropertyName("status")]
    //     public OrderStatus Status { get; set; }
    // }

    public class OrderResponseDto
    {
        [JsonPropertyName("order_id")]
        public int OrderId { get; set; }
        [JsonPropertyName("session_id")]
        public int SessionId { get; set; }
        [JsonPropertyName("bill_id")]
        public int BillId { get; set; }
        [JsonPropertyName("user_id")]
        public int? UserId { get; set; }
        [JsonPropertyName("user_name")]
        public string? UserName { get; set; }
        [JsonPropertyName("status")]
        public OrderStatus Status { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
        [JsonPropertyName("completed_at")]
        public DateTime? CompletedAt { get; set; }

        [JsonPropertyName("order_items")]
        public List<OrderItemResponseDto> OrderItems { get; set; } = new();

        // Automatically calculated from OrderItems
        [JsonPropertyName("pending_items_count")]
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