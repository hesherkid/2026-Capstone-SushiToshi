using System.Text.Json.Serialization;

namespace back_end.Dtos
{
    public class OrderTimingResponseDto
    {
        [JsonPropertyName("range")]
        public AnalyticsRangeDto Range { get; set; } = new();

        [JsonPropertyName("location")]
        public AnalyticsLocationDto? Location { get; set; }

        [JsonPropertyName("summary")]
        public OrderTimingSummaryDto Summary { get; set; } = new();

        [JsonPropertyName("session_duration")]
        public DurationSummaryDto SessionDuration { get; set; } = new();

        [JsonPropertyName("peak_hour")]
        public OrderingHourDto? PeakHour { get; set; }

        [JsonPropertyName("orders_by_hour")]
        public List<OrderingHourDto> OrdersByHour { get; set; } = new();

        [JsonPropertyName("time_to_first_order")]
        public DurationSummaryDto TimeToFirstOrder { get; set; } = new();

        [JsonPropertyName("gap_between_orders")]
        public DurationSummaryDto GapBetweenOrders { get; set; } = new();

        [JsonPropertyName("average_orders_per_session")]
        public decimal? AverageOrdersPerSession { get; set; }

        [JsonPropertyName("pending_to_processing")]
        public DurationSummaryDto PendingToProcessing { get; set; } = new();

        [JsonPropertyName("processing_to_completed")]
        public DurationSummaryDto ProcessingToCompleted { get; set; } = new();

        [JsonPropertyName("slowest_items")]
        public List<ItemTimingDto> SlowestItems { get; set; } = new();

        [JsonPropertyName("fastest_items")]
        public List<ItemTimingDto> FastestItems { get; set; } = new();

        [JsonPropertyName("filters")]
        public AnalyticsFiltersDto Filters { get; set; } = new();
    }

    public class OrderTimingSummaryDto
    {
        [JsonPropertyName("sessions_started")]
        public int SessionsStarted { get; set; }

        [JsonPropertyName("sessions_open")]
        public int SessionsOpen { get; set; }

        [JsonPropertyName("sessions_over_time_limit")]
        public int SessionsOverTimeLimit { get; set; }

        [JsonPropertyName("time_limit_minutes")]
        public int TimeLimitMinutes { get; set; }

        [JsonPropertyName("orders_placed")]
        public int OrdersPlaced { get; set; }

        [JsonPropertyName("orders_missing_approval_time")]
        public int OrdersMissingApprovalTime { get; set; }

        [JsonPropertyName("items_ranked")]
        public int ItemsRanked { get; set; }

        [JsonPropertyName("min_deliveries")]
        public int MinDeliveries { get; set; }
    }

    public class OrderingHourDto
    {
        [JsonPropertyName("hour")]
        public int Hour { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("orders")]
        public int Orders { get; set; }
    }

    public class ItemTimingDto
    {
        [JsonPropertyName("item_id")]
        public int ItemId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("category_id")]
        public int CategoryId { get; set; }

        [JsonPropertyName("category_name")]
        public string CategoryName { get; set; } = string.Empty;

        [JsonPropertyName("tags")]
        public List<AnalyticsTagDto> Tags { get; set; } = new();

        [JsonPropertyName("deliveries")]
        public int Deliveries { get; set; }

        [JsonPropertyName("average_minutes")]
        public decimal AverageMinutes { get; set; }

        [JsonPropertyName("median_minutes")]
        public decimal MedianMinutes { get; set; }

        /// <summary>1 = slowest.</summary>
        [JsonPropertyName("rank")]
        public int Rank { get; set; }
    }
}