using System.Text.Json.Serialization;

namespace back_end.DTO.Analytics
{

    public class ItemPerformanceResponseDTO
    {
        [JsonPropertyName("range")]
        public AnalyticsRangeDTO Range { get; set; } = new();

        [JsonPropertyName("location")]
        public AnalyticsLocationDTO? Location { get; set; }

        [JsonPropertyName("summary")]
        public ItemPerformanceSummaryDTO Summary { get; set; } = new();

        [JsonPropertyName("best")]
        public List<ItemPerformanceDTO> Best { get; set; } = new();

        [JsonPropertyName("worst")]
        public List<ItemPerformanceDTO> Worst { get; set; } = new();

        [JsonPropertyName("zero_sales")]
        public List<ItemPerformanceDTO> ZeroSales { get; set; } = new();

        [JsonPropertyName("add_ons")]
        public List<ItemPerformanceDTO> AddOns { get; set; } = new();

        [JsonPropertyName("items")]
        public List<ItemPerformanceDTO> Items { get; set; } = new();

        [JsonPropertyName("filters")]
        public AnalyticsFiltersDTO Filters { get; set; } = new();
    }

    public class ItemPerformanceSummaryDTO
    {
        [JsonPropertyName("total_units_sold")]
        public int TotalUnitsSold { get; set; }

        [JsonPropertyName("total_add_on_units")]
        public int TotalAddOnUnits { get; set; }

        [JsonPropertyName("items_considered")]
        public int ItemsConsidered { get; set; }

        [JsonPropertyName("items_with_sales")]
        public int ItemsWithSales { get; set; }

        [JsonPropertyName("items_with_zero_sales")]
        public int ItemsWithZeroSales { get; set; }
    }

    public class ItemPerformanceDTO
    {
        [JsonPropertyName("item_id")]
        public int ItemId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("category_id")]
        public int CategoryId { get; set; }

        [JsonPropertyName("category_name")]
        public string CategoryName { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("currently_available")]
        public bool CurrentlyAvailable { get; set; }

        [JsonPropertyName("tags")]
        public List<AnalyticsTagDTO> Tags { get; set; } = new();

        [JsonPropertyName("units_sold")]
        public int UnitsSold { get; set; }

        [JsonPropertyName("share_of_units")]
        public decimal ShareOfUnits { get; set; }

        [JsonPropertyName("add_on_units")]
        public int AddOnUnits { get; set; }

        [JsonPropertyName("add_on_share")]
        public decimal AddOnShare { get; set; }

        [JsonPropertyName("add_on_days")]
        public int AddOnDays { get; set; }

        [JsonPropertyName("rank")]
        public int? Rank { get; set; }
    }
}