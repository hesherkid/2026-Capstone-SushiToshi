using System.Text.Json.Serialization;

namespace back_end.Dtos
{
  public class CreateBill
  {
    public string? BillName { get; set; }
    public int SeniorCount { get; set; }

    public int AdultCount { get; set; }

    public int ChildCount { get; set; }

    public int TotCount { get; set; }

    public int? TotalCount { get; set; }
  }

  public class BillResponse
  {
    public int BillId { get; set; }

    public int SessionId { get; set; }

    public string BillName { get; set; } = string.Empty;

    public int SeniorCount { get; set; }

    public int AdultCount { get; set; }

    public int ChildCount { get; set; }

    public int TotCount { get; set; } // toddler

    public int TotalCount { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    // Set once the guests ask for the bill; ordering on it stops
    public DateTime? RequestedAt { get; set; }

    public List<int> TableNumbers { get; set; } = new List<int>();
  }

  public class BillSummaryResponse
  {
    public int BillId { get; set; }

    public int SessionId { get; set; }

    public string BillName { get; set; } = string.Empty;

    // Guest counts
    public int AdultCount { get; set; }

    public int SeniorCount { get; set; }

    public int ChildCount { get; set; }

    public int TotCount { get; set; }

    public int TotalCount { get; set; }

    // Base pricing breakdown
    public decimal AdultBasePrice { get; set; }

    public decimal SeniorBasePrice { get; set; }

    public decimal ChildBasePrice { get; set; }

    public decimal ToddlerBasePrice { get; set; }

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

    public List<BillAddOnItem> LeftoverItems { get; set; } = new List<BillAddOnItem>();

    public decimal LeftoverCharge { get; set; }

    // Totals
    public decimal Subtotal { get; set; }

    // Guests at the whole table (every bill in the session); decides the gratuity
    public int PartySize { get; set; }

    // 0 unless the party is big enough
    public decimal GratuityRate { get; set; }

    public decimal GratuityAmount { get; set; }

    // Guests it takes for the gratuity to apply (0 = never), so the bill can explain it
    public int GratuityMinPartySize { get; set; }

    // GST, on the subtotal and any gratuity
    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    // Status
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    // Set once the guests ask for the bill; ordering on it stops
    public DateTime? RequestedAt { get; set; }

    public List<int> TableNumbers { get; set; } = new List<int>();

    public string PricingType { get; set; } = string.Empty;  // "Weekday", "Weekend", or "Holiday"
    // e.g. "Canada Day" when Pricing_Type is "Holiday"
    public string? HolidayName { get; set; }

    // True once the bill has closed: its prices no longer follow pricing changes
    public bool PricesLocked { get; set; }
  }

  public class BillAddOnItem
  {
    public int OrderItemId { get; set; }

    public int ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal PricePerUnit { get; set; }

    public decimal LineTotal { get; set; }

    public string Status { get; set; } = string.Empty;
  }

  public class LeftoverFeeRequest
  {
    public decimal? Amount { get; set; }
  }

  public class LeftoverFeeResponse
  {
    public int SessionId { get; set; }

    // The default the dialog starts with (from pricing)
    public decimal DefaultFee { get; set; }

    // The table's current fee: the open bills' shares added up
    public decimal Amount { get; set; }

    public List<LeftoverFeeBillShare> Bills { get; set; } = new List<LeftoverFeeBillShare>();
  }

  public class LeftoverFeeBillShare
  {
    public int BillId { get; set; }

    public string BillName { get; set; } = string.Empty;

    public decimal LeftoverCharge { get; set; }
  }
}