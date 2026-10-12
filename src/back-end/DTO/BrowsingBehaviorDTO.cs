using System.Text.Json.Serialization;

namespace back_end.DTO.Analytics
{
    // GET api/analytics/browsing-behavior.

    public class BrowsingBehaviorResponseDTO
    {
        [JsonPropertyName("range")]
        public AnalyticsRangeDTO Range { get; set; } = new();

        [JsonPropertyName("location")]
        public AnalyticsLocationDTO? Location { get; set; }

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

        [JsonPropertyName("items")]
        public List<BrowsingItemDTO> Items { get; set; } = new();

        [JsonPropertyName("filters")]
        public AnalyticsFiltersDTO Filters { get; set; } = new();
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
        public List<AnalyticsTagDTO> Tags { get; set; } = new();

        [JsonPropertyName("views")]
        public int Views { get; set; }

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
}