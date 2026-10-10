using System.Text.Json.Serialization;

namespace back_end.DTO.Analytics
{
    public class BrowsingBehaviorResponseDTO
    {
        [JsonPropertyName("range")]
        public ItemPerformanceRangeDTO Range { get; set; } = new();

        [JsonPropertyName("location")]
        public ItemPerformanceLocationDTO? Location { get; set; }

        [JsonPropertyName("summary")]
        public BrowsingSummaryDTO Summary { get; set; } = new();

        [JsonPropertyName("most_viewed")]
        public List<BrowsingItemDTO> MostViewed { get; set; } = new();

        [JsonPropertyName("least_viewed")]
        public List<BrowsingItemDTO> LeastViewed { get; set; } = new();

        [JsonPropertyName("never_viewed")]
        public List<BrowsingItemDTO> NeverViewed { get; set; } = new();

        [JsonPropertyName("viewed_not_ordered")]
        public List<BrowsingItemDTO> ViewedNotOrdered { get; set; } = new();

        [JsonPropertyName("top_viewed_not_ordered")]
        public List<BrowsingItemDTO> TopViewedNotOrdered { get; set; } = new();

        [JsonPropertyName("top_viewed_and_ordered")]
        public List<BrowsingItemDTO> TopViewedAndOrdered { get; set; } = new();

        [JsonPropertyName("low_conversion_high_views")]
        public List<BrowsingItemDTO> LowConversionHighViews { get; set; } = new();

        [JsonPropertyName("unavailable_views")]
        public List<BrowsingItemDTO> UnavailableViews { get; set; } = new();

        /// <summary>Every included item: viewed items by views, then unviewed items A-Z.</summary>
        [JsonPropertyName("items")]
        public List<BrowsingItemDTO> Items { get; set; } = new();

        [JsonPropertyName("filters")]
        public ItemPerformanceFiltersDTO Filters { get; set; } = new();
    }

    public class BrowsingSummaryDTO
    {
        [JsonPropertyName("total_views")]
        public int TotalViews { get; set; }

        [JsonPropertyName("short_views_excluded")]
        public int ShortViewsExcluded { get; set; }

        [JsonPropertyName("total_view_seconds")]
        public int TotalViewSeconds { get; set; }

        [JsonPropertyName("average_view_seconds")]
        public decimal? AverageViewSeconds { get; set; }

        [JsonPropertyName("average_view_seconds_ordered")]
        public decimal? AverageViewSecondsOrdered { get; set; }

        [JsonPropertyName("average_view_seconds_not_ordered")]
        public decimal? AverageViewSecondsNotOrdered { get; set; }

        [JsonPropertyName("viewing_sessions")]
        public int ViewingSessions { get; set; }

        [JsonPropertyName("session_item_views")]
        public int SessionItemViews { get; set; }

        [JsonPropertyName("session_item_orders")]
        public int SessionItemOrders { get; set; }

        [JsonPropertyName("conversion_rate")]
        public decimal? ConversionRate { get; set; }

        [JsonPropertyName("items_considered")]
        public int ItemsConsidered { get; set; }

        [JsonPropertyName("items_viewed")]
        public int ItemsViewed { get; set; }

        [JsonPropertyName("items_never_viewed")]
        public int ItemsNeverViewed { get; set; }

        [JsonPropertyName("unavailable_views")]
        public int UnavailableViews { get; set; }

        [JsonPropertyName("min_view_seconds")]
        public int MinViewSeconds { get; set; }

        [JsonPropertyName("min_views")]
        public int MinViews { get; set; }

        /// <summary>Median views across viewed items. 0 when nothing was viewed.</summary>
        [JsonPropertyName("median_views")]
        public decimal MedianViews { get; set; }

        /// <summary>Views needed for low_conversion_high_views: the larger of min_views and the median (rounded up).</summary>
        [JsonPropertyName("high_views_threshold")]
        public int HighViewsThreshold { get; set; }
    }

    public class BrowsingItemDTO
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
        public List<ItemPerformanceTagDTO> Tags { get; set; } = new();

        [JsonPropertyName("views")]
        public int Views { get; set; }

        /// <summary>Competition rank by views (1, 2, 2, 4). Null when not viewed.</summary>
        [JsonPropertyName("view_rank")]
        public int? ViewRank { get; set; }

        [JsonPropertyName("short_views")]
        public int ShortViews { get; set; }

        [JsonPropertyName("total_view_seconds")]
        public int TotalViewSeconds { get; set; }

        [JsonPropertyName("average_view_seconds")]
        public decimal? AverageViewSeconds { get; set; }

        [JsonPropertyName("viewing_sessions")]
        public int ViewingSessions { get; set; }

        [JsonPropertyName("ordering_sessions")]
        public int OrderingSessions { get; set; }

        [JsonPropertyName("conversion_rate")]
        public decimal? ConversionRate { get; set; }

        [JsonPropertyName("units_ordered_after_view")]
        public int UnitsOrderedAfterView { get; set; }

        [JsonPropertyName("average_view_seconds_ordered")]
        public decimal? AverageViewSecondsOrdered { get; set; }

        [JsonPropertyName("average_view_seconds_not_ordered")]
        public decimal? AverageViewSecondsNotOrdered { get; set; }

        [JsonPropertyName("unavailable_views")]
        public int UnavailableViews { get; set; }
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

        [JsonPropertyName("best")]
        public List<ItemPerformanceDTO> Best { get; set; } = new();

        [JsonPropertyName("worst")]
        public List<ItemPerformanceDTO> Worst { get; set; } = new();

        [JsonPropertyName("zero_sales")]
        public List<ItemPerformanceDTO> ZeroSales { get; set; } = new();

        /// <summary>Items sold as add-ons, most consistent first (add_on_days, then add_on_units).</summary>
        [JsonPropertyName("add_ons")]
        public List<ItemPerformanceDTO> AddOns { get; set; } = new();

        [JsonPropertyName("items")]
        public List<ItemPerformanceDTO> Items { get; set; } = new();

        [JsonPropertyName("filters")]
        public ItemPerformanceFiltersDTO Filters { get; set; } = new();
    }

    public class ItemPerformanceRangeDTO
    {
        [JsonPropertyName("preset")]
        public string Preset { get; set; } = string.Empty;

        /// <summary>Inclusive local date, yyyy-MM-dd.</summary>
        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty;

        /// <summary>Inclusive local date, yyyy-MM-dd.</summary>
        [JsonPropertyName("to")]
        public string To { get; set; } = string.Empty;

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