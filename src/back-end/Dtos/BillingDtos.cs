using System.Text.Json.Serialization;

namespace back_end.Dtos
{
  public class CreateBill
  {
    [JsonPropertyName("bill_name")]
    public string? BillName { get; set; }
    [JsonPropertyName("senior_count")]
    public int SeniorCount { get; set; }

    [JsonPropertyName("adult_count")]
    public int AdultCount { get; set; }

    [JsonPropertyName("child_count")]
    public int ChildCount { get; set; }

    [JsonPropertyName("tot_count")]
    public int TotCount { get; set; }

    [JsonPropertyName("total_count")]
    public int? TotalCount { get; set; }
  }

  public class BillResponse
  {
    [JsonPropertyName("bill_id")]
    public int BillId { get; set; }

    [JsonPropertyName("session_id")]
    public int SessionId { get; set; }

    [JsonPropertyName("bill_name")]
    public string BillName { get; set; } = string.Empty;

    [JsonPropertyName("senior_count")]
    public int SeniorCount { get; set; }

    [JsonPropertyName("adult_count")]
    public int AdultCount { get; set; }

    [JsonPropertyName("child_count")]
    public int ChildCount { get; set; }

    [JsonPropertyName("tot_count")]
    public int TotCount { get; set; } // toddler

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("closed_at")]
    public DateTime? ClosedAt { get; set; }

    // Set once the guests ask for the bill; ordering on it stops
    [JsonPropertyName("requested_at")]
    public DateTime? RequestedAt { get; set; }

    [JsonPropertyName("table_numbers")]
    public List<int> TableNumbers { get; set; } = new List<int>();
  }

  public class BillSummaryResponse
  {
    [JsonPropertyName("bill_id")]
    public int BillId { get; set; }

    [JsonPropertyName("session_id")]
    public int SessionId { get; set; }

    [JsonPropertyName("bill_name")]
    public string BillName { get; set; } = string.Empty;

    // Guest counts
    [JsonPropertyName("adult_count")]
    public int AdultCount { get; set; }

    [JsonPropertyName("senior_count")]
    public int SeniorCount { get; set; }

    [JsonPropertyName("child_count")]
    public int ChildCount { get; set; }

    [JsonPropertyName("tot_count")]
    public int TotCount { get; set; }

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }

    // Base pricing breakdown
    [JsonPropertyName("adult_base_price")]
    public decimal AdultBasePrice { get; set; }

    [JsonPropertyName("senior_base_price")]
    public decimal SeniorBasePrice { get; set; }

    [JsonPropertyName("child_base_price")]
    public decimal ChildBasePrice { get; set; }

    [JsonPropertyName("toddler_base_price")]
    public decimal ToddlerBasePrice { get; set; }

    [JsonPropertyName("base_charges_subtotal")]
    public decimal BaseChargesSubtotal { get; set; }

    // Add-on items
    [JsonPropertyName("addon_items")]
    public List<BillAddOnItem> AddOnItems { get; set; } = new List<BillAddOnItem>();

    [JsonPropertyName("addon_subtotal")]
    public decimal AddOnSubtotal { get; set; }

    [JsonPropertyName("overlimit_items")]
    public List<BillAddOnItem> OverLimitItems { get; set; } = new List<BillAddOnItem>();

    [JsonPropertyName("overlimit_subtotal")]
    public decimal OverLimitSubtotal { get; set; }

    [JsonPropertyName("leftover_items")]
    public List<BillAddOnItem> LeftoverItems { get; set; } = new List<BillAddOnItem>();

    [JsonPropertyName("leftover_charge")]
    public decimal LeftoverCharge { get; set; }

    // Totals
    [JsonPropertyName("subtotal")]
    public decimal Subtotal { get; set; }

    // Guests at the whole table (every bill in the session); decides the gratuity
    [JsonPropertyName("party_size")]
    public int PartySize { get; set; }

    // 0 unless the party is big enough
    [JsonPropertyName("gratuity_rate")]
    public decimal GratuityRate { get; set; }

    [JsonPropertyName("gratuity_amount")]
    public decimal GratuityAmount { get; set; }

    // Guests it takes for the gratuity to apply (0 = never), so the bill can explain it
    [JsonPropertyName("gratuity_min_party_size")]
    public int GratuityMinPartySize { get; set; }

    // GST, on the subtotal and any gratuity
    [JsonPropertyName("tax_rate")]
    public decimal TaxRate { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("total_amount")]
    public decimal TotalAmount { get; set; }

    // Status
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("closed_at")]
    public DateTime? ClosedAt { get; set; }

    // Set once the guests ask for the bill; ordering on it stops
    [JsonPropertyName("requested_at")]
    public DateTime? RequestedAt { get; set; }

    [JsonPropertyName("table_numbers")]
    public List<int> TableNumbers { get; set; } = new List<int>();

    [JsonPropertyName("pricing_type")]
    public string PricingType { get; set; } = string.Empty;  // "Weekday", "Weekend", or "Holiday"
    // e.g. "Canada Day" when Pricing_Type is "Holiday"
    [JsonPropertyName("holiday_name")]
    public string? HolidayName { get; set; }

    // True once the bill has closed: its prices no longer follow pricing changes
    [JsonPropertyName("prices_locked")]
    public bool PricesLocked { get; set; }
  }

  public class BillAddOnItem
  {
    [JsonPropertyName("order_item_id")]
    public int OrderItemId { get; set; }

    [JsonPropertyName("item_id")]
    public int ItemId { get; set; }

    [JsonPropertyName("item_name")]
    public string ItemName { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("price_per_unit")]
    public decimal PricePerUnit { get; set; }

    [JsonPropertyName("line_total")]
    public decimal LineTotal { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
  }

  public class LeftoverFeeRequest
  {
    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }
  }

  public class LeftoverFeeResponse
  {
    [JsonPropertyName("session_id")]
    public int SessionId { get; set; }

    // The default the dialog starts with (from pricing)
    [JsonPropertyName("default_fee")]
    public decimal DefaultFee { get; set; }

    // The table's current fee: the open bills' shares added up
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("bills")]
    public List<LeftoverFeeBillShare> Bills { get; set; } = new List<LeftoverFeeBillShare>();
  }

  public class LeftoverFeeBillShare
  {
    [JsonPropertyName("bill_id")]
    public int BillId { get; set; }

    [JsonPropertyName("bill_name")]
    public string BillName { get; set; } = string.Empty;

    [JsonPropertyName("leftover_charge")]
    public decimal LeftoverCharge { get; set; }
  }
}