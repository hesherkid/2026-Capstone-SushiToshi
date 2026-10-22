using System.Text.Json.Serialization;

namespace back_end.DTO.bill
{
  public class CreateBill
  {
    [JsonPropertyName("bill_name")]
    public string? Bill_Name { get; set; }
    [JsonPropertyName("senior_count")]
    public int Senior_Count { get; set; }

    [JsonPropertyName("adult_count")]
    public int Adult_Count { get; set; }

    [JsonPropertyName("child_count")]
    public int Child_Count { get; set; }

    [JsonPropertyName("tot_count")]
    public int Tot_Count { get; set; }

    [JsonPropertyName("total_count")]
    public int? Total_Count { get; set; }
  }

  public class BillResponse
  {
    [JsonPropertyName("bill_id")]
    public int Bill_Id { get; set; }

    [JsonPropertyName("session_id")]
    public int Session_Id { get; set; }

    [JsonPropertyName("bill_name")]
    public string Bill_Name { get; set; } = string.Empty;

    [JsonPropertyName("senior_count")]
    public int Senior_Count { get; set; }

    [JsonPropertyName("adult_count")]
    public int Adult_Count { get; set; }

    [JsonPropertyName("child_count")]
    public int Child_Count { get; set; }

    [JsonPropertyName("tot_count")]
    public int Tot_Count { get; set; } // toddler

    [JsonPropertyName("total_count")]
    public int Total_Count { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime Created_At { get; set; }

    [JsonPropertyName("closed_at")]
    public DateTime? Closed_At { get; set; }

    // Set once the guests ask for the bill; ordering on it stops
    [JsonPropertyName("requested_at")]
    public DateTime? Requested_At { get; set; }

    [JsonPropertyName("table_numbers")]
    public List<int> Table_Numbers { get; set; } = new List<int>();
  }

  public class BillSummaryResponse
  {
    [JsonPropertyName("bill_id")]
    public int Bill_Id { get; set; }

    [JsonPropertyName("session_id")]
    public int Session_Id { get; set; }

    [JsonPropertyName("bill_name")]
    public string Bill_Name { get; set; } = string.Empty;

    // Guest counts
    [JsonPropertyName("adult_count")]
    public int Adult_Count { get; set; }

    [JsonPropertyName("senior_count")]
    public int Senior_Count { get; set; }

    [JsonPropertyName("child_count")]
    public int Child_Count { get; set; }

    [JsonPropertyName("tot_count")]
    public int Tot_Count { get; set; }

    [JsonPropertyName("total_count")]
    public int Total_Count { get; set; }

    // Base pricing breakdown
    [JsonPropertyName("adult_base_price")]
    public decimal Adult_Base_Price { get; set; }

    [JsonPropertyName("senior_base_price")]
    public decimal Senior_Base_Price { get; set; }

    [JsonPropertyName("child_base_price")]
    public decimal Child_Base_Price { get; set; }

    [JsonPropertyName("toddler_base_price")]
    public decimal Toddler_Base_Price { get; set; }

    [JsonPropertyName("base_charges_subtotal")]
    public decimal Base_Charges_Subtotal { get; set; }

    // Add-on items
    [JsonPropertyName("addon_items")]
    public List<BillAddOnItem> AddOn_Items { get; set; } = new List<BillAddOnItem>();

    [JsonPropertyName("addon_subtotal")]
    public decimal AddOn_Subtotal { get; set; }

    [JsonPropertyName("overlimit_items")]
    public List<BillAddOnItem> OverLimit_Items { get; set; } = new List<BillAddOnItem>();

    [JsonPropertyName("overlimit_subtotal")]
    public decimal OverLimit_Subtotal { get; set; }

    [JsonPropertyName("leftover_items")]
    public List<BillAddOnItem> Leftover_Items { get; set; } = new List<BillAddOnItem>();

    [JsonPropertyName("leftover_charge")]
    public decimal Leftover_Charge { get; set; }

    // Totals
    [JsonPropertyName("subtotal")]
    public decimal Subtotal { get; set; }

    [JsonPropertyName("tax_rate")]
    public decimal Tax_Rate { get; set; }

    [JsonPropertyName("tax_amount")]
    public decimal Tax_Amount { get; set; }

    [JsonPropertyName("total_amount")]
    public decimal Total_Amount { get; set; }

    // Status
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime Created_At { get; set; }

    [JsonPropertyName("closed_at")]
    public DateTime? Closed_At { get; set; }

    // Set once the guests ask for the bill; ordering on it stops
    [JsonPropertyName("requested_at")]
    public DateTime? Requested_At { get; set; }

    [JsonPropertyName("table_numbers")]
    public List<int> Table_Numbers { get; set; } = new List<int>();

    [JsonPropertyName("pricing_type")]
    public string Pricing_Type { get; set; } = string.Empty;  // "Weekday", "Weekend", or "Holiday"
  }

  public class BillAddOnItem
  {
    [JsonPropertyName("order_item_id")]
    public int Order_Item_Id { get; set; }

    [JsonPropertyName("item_id")]
    public int Item_Id { get; set; }

    [JsonPropertyName("item_name")]
    public string Item_Name { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("price_per_unit")]
    public decimal Price_Per_Unit { get; set; }

    [JsonPropertyName("line_total")]
    public decimal Line_Total { get; set; }

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
    public int Session_Id { get; set; }

    // The default the dialog starts with (from pricing)
    [JsonPropertyName("default_fee")]
    public decimal Default_Fee { get; set; }

    // The table's current fee: the open bills' shares added up
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("bills")]
    public List<LeftoverFeeBillShare> Bills { get; set; } = new List<LeftoverFeeBillShare>();
  }

  public class LeftoverFeeBillShare
  {
    [JsonPropertyName("bill_id")]
    public int Bill_Id { get; set; }

    [JsonPropertyName("bill_name")]
    public string Bill_Name { get; set; } = string.Empty;

    [JsonPropertyName("leftover_charge")]
    public decimal Leftover_Charge { get; set; }
  }
}