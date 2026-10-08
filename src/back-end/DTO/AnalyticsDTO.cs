using System.Text.Json.Serialization;

namespace back_end.DTO.Analytics
{
    public class BrowsingBehaviorDTO
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int ViewCount { get; set; }

        public int totalViewTimes { get; set; }
    }

    public class TableTurnoverDailyDTO
    {
        public int Party_size { get; set; }
        public string Day { get; set; } = string.Empty;
        public int AverageDuration { get; set; }

    }
    public class TableTurnOverMonthlyDTO
    {
        public int Party_size { get; set; }
        public string Month { get; set; } = string.Empty;
        public int AverageDuration { get; set; }
    }

    public class OrderTimingDTO
    {
        public int SessionId { get; set; }
        public int TimeToFirstOrder { get; set; }

    }

    public class TableTurnOverResponseDTO
    {
        public List<TableTurnoverDailyDTO> Daily { get; set; } = new List<TableTurnoverDailyDTO>();
        public List<TableTurnOverMonthlyDTO> Monthly { get; set; } = new List<TableTurnOverMonthlyDTO>();

    }

    public class DailyAverageTimingDTO
    {
        public string Day { get; set; } = string.Empty;
        public double AverageTimeToFirstOrder { get; set; }

    }

    public class OrderTimingResponseDTO
    {
        public List<OrderTimingDTO> OrderTimings { get; set; } = new List<OrderTimingDTO>();
        public List<DailyAverageTimingDTO> DailyAverages { get; set; } = new List<DailyAverageTimingDTO>();
    }
    public class TurnoverMetricDTO
    {
        public string? Period { get; set; }
        public int AverageDuration { get; set; }
        public int PartySize { get; set; }
    }

    public class ItemPerformanceResponseDTO
    {
        [JsonPropertyName("range")]
        public ItemPerformanceRangeDTO Range { get; set; } = new();

        /// <summary>Null when results cover all locations.</summary>
        [JsonPropertyName("location")]
        public ItemPerformanceLocationDTO? Location { get; set; }

        [JsonPropertyName("summary")]
        public ItemPerformanceSummaryDTO Summary { get; set; } = new();

        /// <summary>Highest units sold first. Items with zero sales are never included.</summary>
        [JsonPropertyName("best")]
        public List<ItemPerformanceDTO> Best { get; set; } = new();

        /// <summary>Lowest units sold first (above zero). Never overlaps with Best.</summary>
        [JsonPropertyName("worst")]
        public List<ItemPerformanceDTO> Worst { get; set; } = new();

        /// <summary>Eligible items that sold nothing in the range, A-Z.</summary>
        [JsonPropertyName("zero_sales")]
        public List<ItemPerformanceDTO> ZeroSales { get; set; } = new();

        /// <summary>Items sold as add-ons, most consistent first (add_on_days, then add_on_units).</summary>
        [JsonPropertyName("add_ons")]
        public List<ItemPerformanceDTO> AddOns { get; set; } = new();

        /// <summary>Every included item: ranked items first, then zero-sales items.</summary>
        [JsonPropertyName("items")]
        public List<ItemPerformanceDTO> Items { get; set; } = new();

        /// <summary>Options for the category / tag dropdowns (ignores the current category/tag filter).</summary>
        [JsonPropertyName("filters")]
        public ItemPerformanceFiltersDTO Filters { get; set; } = new();
    }

    public class ItemPerformanceRangeDTO
    {
        /// <summary>today | yesterday | last7 | last30 | custom</summary>
        [JsonPropertyName("preset")]
        public string Preset { get; set; } = string.Empty;

        /// <summary>Inclusive local date, yyyy-MM-dd.</summary>
        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty;

        /// <summary>Inclusive local date, yyyy-MM-dd.</summary>
        [JsonPropertyName("to")]
        public string To { get; set; } = string.Empty;

        /// <summary>Number of local days in the window (1 for today / yesterday).</summary>
        [JsonPropertyName("days")]
        public int Days { get; set; }

        [JsonPropertyName("time_zone")]
        public string TimeZone { get; set; } = string.Empty;
    }

    public class ItemPerformanceLocationDTO
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
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

        /// <summary>Current item status: Available | Seasonal | Unavailable.</summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// False when the item is unavailable now or no longer on an active menu (at the location).
        /// It's still included because it sold during the window.
        /// </summary>
        [JsonPropertyName("currently_available")]
        public bool CurrentlyAvailable { get; set; }

        [JsonPropertyName("tags")]
        public List<ItemPerformanceTagDTO> Tags { get; set; } = new();

        [JsonPropertyName("units_sold")]
        public int UnitsSold { get; set; }

        /// <summary>Percent of all units sold in the result set, 1 decimal place.</summary>
        [JsonPropertyName("share_of_units")]
        public decimal ShareOfUnits { get; set; }

        /// <summary>Units of this item that were ordered as an add-on.</summary>
        [JsonPropertyName("add_on_units")]
        public int AddOnUnits { get; set; }

        /// <summary>Percent of this item's units that were add-ons, 1 decimal place.</summary>
        [JsonPropertyName("add_on_share")]
        public decimal AddOnShare { get; set; }

        /// <summary>Local days in the window with at least one add-on sale of this item.</summary>
        [JsonPropertyName("add_on_days")]
        public int AddOnDays { get; set; }

        /// <summary>Competition rank (1, 2, 2, 4). Null for zero-sales items.</summary>
        [JsonPropertyName("rank")]
        public int? Rank { get; set; }
    }

    public class ItemPerformanceTagDTO
    {
        [JsonPropertyName("tag_id")]
        public int TagId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("color")]
        public string Color { get; set; } = string.Empty;
    }

    public class ItemPerformanceFiltersDTO
    {
        [JsonPropertyName("categories")]
        public List<ItemPerformanceFilterOptionDTO> Categories { get; set; } = new();

        [JsonPropertyName("tags")]
        public List<ItemPerformanceTagDTO> Tags { get; set; } = new();
    }

    public class ItemPerformanceFilterOptionDTO
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}