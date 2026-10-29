using System.Text.Json.Serialization;

namespace back_end.DTO.PastVisitDTOs
{
    // One ended visit on the customer's Past visits page
    public class PastVisitDTO
    {
        [JsonPropertyName("session_id")]
        public int SessionId { get; set; }

        [JsonPropertyName("started_at")]
        public DateTime StartedAt { get; set; }

        [JsonPropertyName("ended_at")]
        public DateTime? EndedAt { get; set; }

        [JsonPropertyName("location_name")]
        public string LocationName { get; set; } = string.Empty;

        [JsonPropertyName("table_numbers")]
        public List<int> TableNumbers { get; set; } = [];

        // What this customer ordered, same items added together
        [JsonPropertyName("items")]
        public List<PastVisitItemDTO> Items { get; set; } = [];

        [JsonPropertyName("bills")]
        public List<PastVisitBillDTO> Bills { get; set; } = [];
    }

    public class PastVisitItemDTO
    {
        [JsonPropertyName("item_id")]
        public int ItemId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }
    }

    public class PastVisitBillDTO
    {
        [JsonPropertyName("bill_id")]
        public int BillId { get; set; }

        [JsonPropertyName("bill_name")]
        public string BillName { get; set; } = string.Empty;

        [JsonPropertyName("guest_count")]
        public int GuestCount { get; set; }

        // Including gratuity and GST, as on the bill
        [JsonPropertyName("total")]
        public decimal Total { get; set; }
    }
}
