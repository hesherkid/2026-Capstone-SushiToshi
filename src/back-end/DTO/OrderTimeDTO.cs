using System.Text.Json.Serialization;

namespace back_end.DTO.Analytics
{
    public class OrderTimingResponseDTO
    {
        [JsonPropertyName("range")]
        public AnalyticsRangeDTO Range { get; set; } = new();

        [JsonPropertyName("location")]
        public AnalyticsLocationDTO? Location { get; set; }

        [JsonPropertyName("summary")]
        public OrderTimingSummaryDTO Summary { get; set; } = new();

        [JsonPropertyName("session_duration")]
        public DurationSummaryDTO SessionDuration { get; set; } = new();

        [JsonPropertyName("peak_hour")]
        public OrderingHourDTO? PeakHour { get; set; }

        [JsonPropertyName("orders_by_hour")]
        public List<OrderingHourDTO> OrdersByHour { get; set; } = new();

        [JsonPropertyName("time_to_first_order")]
        public DurationSummaryDTO TimeToFirstOrder { get; set; } = new();

        [JsonPropertyName("gap_between_orders")]
        public DurationSummaryDTO GapBetweenOrders { get; set; } = new();

        [JsonPropertyName("average_orders_per_session")]
        public decimal? AverageOrdersPerSession { get; set; }

        [JsonPropertyName("pending_to_processing")]
        public DurationSummaryDTO PendingToProcessing { get; set; } = new();

        [JsonPropertyName("processing_to_completed")]
        public DurationSummaryDTO ProcessingToCompleted { get; set; } = new();

        [JsonPropertyName("slowest_items")]
        public List<ItemTimingDTO> SlowestItems { get; set; } = new();

        [JsonPropertyName("fastest_items")]
        public List<ItemTimingDTO> FastestItems { get; set; } = new();

        [JsonPropertyName("filters")]
        public AnalyticsFiltersDTO Filters { get; set; } = new();
    }

    public class OrderTimingSummaryDTO
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

    public class OrderingHourDTO
    {
        [JsonPropertyName("hour")]
        public int Hour { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("orders")]
        public int Orders { get; set; }
    }

    public class ItemTimingDTO
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
        public List<AnalyticsTagDTO> Tags { get; set; } = new();

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