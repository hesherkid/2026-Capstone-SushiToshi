using System.Text.Json.Serialization;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Helpers;

namespace back_end.Dtos
{
    // An order line as guests and staff see it: the price shows only on lines they pay for.
    // Keys are fixed so they match the item shape the front end reads, whatever the serializer settings
    public class PricedOrderItemDto
    {
        [JsonPropertyName("order_item_id")]
        public int OrderItemId { get; set; }
        [JsonPropertyName("item_id")]
        public int ItemId { get; set; }
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }
        [JsonPropertyName("price_at_time")]
        public decimal PriceAtTime { get; set; }
        [JsonPropertyName("is_add_on")]
        public bool IsAddOn { get; set; }
        [JsonPropertyName("charge_reason")]
        public string ChargeReason { get; set; } = string.Empty;
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }

    public static class OrderMappings
    {
        // name: for lines whose MenuItem isn't loaded (a new order); otherwise the menu item's name
        public static OrderItemResponseDto ToResponseDto(this OrderItems item, string? name = null) => new()
        {
            OrderItemId = item.Order_Item_Id,
            OrderId = item.Order_Key,
            MenuId = item.Menu_Id,
            ItemId = item.Item_Id,
            Quantity = item.Quantity,
            PriceAtTime = item.Price_At_Time,
            Status = item.Order_Item_Status,
            CompletedAt = item.Completed_At,
            Name = name ?? item.MenuItem?.Name,
            ChargeReason = item.Charge_Reason.ToString()
        };

        public static OrderResponseDto ToResponseDto(this SessionOrder order, IReadOnlyDictionary<int, string>? namesById = null) => new()
        {
            OrderId = order.Order_Id,
            SessionId = order.session_id,
            BillId = order.Bill_Id,
            UserId = order.User_Id,
            UserName = order.User_Name,
            Status = order.Status,
            CreatedAt = order.Created_At,
            CompletedAt = order.Completed_At,
            OrderItems = order.OrderItems
                .Select(oi => oi.ToResponseDto(namesById?.GetValueOrDefault(oi.Item_Id)))
                .ToList()
        };

        // The staff order shape, shared by all-orders and order history so the two can't drift apart.
        // Needs the includes from StaffOrderQuery.WithStaffOrderDetails
        public static object ToStaffOrder(this SessionOrder o) => new
        {
            order_id = o.Order_Id,
            session_id = o.session_id,
            bill_id = o.Bill_Id,
            bill_name = o.Bill.Bill_Name,
            location_id = o.DiningSession.Location_Id,
            table_numbers = o.DiningSession.Table_Id.HasValue
                ? new[] { o.DiningSession.Table!.table_number }
                : o.DiningSession.TableGroup?.Tables?.Select(t => t.table_number).ToArray() ?? Array.Empty<int>(),
            user_name = o.User_Name,
            status = o.Status.ToString(),
            created_at = o.Created_At,
            completed_at = o.Completed_At,
            items = o.OrderItems.Select(oi => oi.ToPricedItem(o.DiningSession.Location_Id)).ToList()
        };

        // Needs MenuItem.MenuAssignments loaded. Add-ons and over-limit lines show their price; the rest are 0
        public static PricedOrderItemDto ToPricedItem(this OrderItems item, int? locationId)
        {
            var isAddOn = item.MenuItem?.MenuAssignments
                .EffectiveFor(item.Menu_Id, item.Item_Id, locationId)?.Is_Add_On ?? false;

            return new PricedOrderItemDto
            {
                OrderItemId = item.Order_Item_Id,
                ItemId = item.Item_Id,
                Name = item.MenuItem?.Name,
                Quantity = item.Quantity,
                PriceAtTime = isAddOn || item.Charge_Reason == ChargeReason.OverLimit ? item.Price_At_Time : 0,
                IsAddOn = isAddOn,
                ChargeReason = item.Charge_Reason.ToString(),
                Status = item.Order_Item_Status.ToString()
            };
        }
    }
}
