
namespace back_end.Dtos.Analytics
{
    public class OrderTimingResponseDto
    {
        public AnalyticsRangeDto Range { get; set; } = new();

        public AnalyticsLocationDto? Location { get; set; }

        public OrderTimingSummaryDto Summary { get; set; } = new();

        public DurationSummaryDto SessionDuration { get; set; } = new();

        public OrderingHourDto? PeakHour { get; set; }

        public List<OrderingHourDto> OrdersByHour { get; set; } = new();

        public DurationSummaryDto TimeToFirstOrder { get; set; } = new();

        public DurationSummaryDto GapBetweenOrders { get; set; } = new();

        public decimal? AverageOrdersPerSession { get; set; }

        public DurationSummaryDto PendingToProcessing { get; set; } = new();

        public DurationSummaryDto ProcessingToCompleted { get; set; } = new();

        public List<ItemTimingDto> SlowestItems { get; set; } = new();

        public List<ItemTimingDto> FastestItems { get; set; } = new();

        public AnalyticsFiltersDto Filters { get; set; } = new();
    }

    public class OrderTimingSummaryDto
    {
        public int SessionsStarted { get; set; }

        public int SessionsOpen { get; set; }

        public int SessionsOverTimeLimit { get; set; }

        public int TimeLimitMinutes { get; set; }

        public int OrdersPlaced { get; set; }

        public int OrdersMissingApprovalTime { get; set; }

        public int ItemsRanked { get; set; }

        public int MinDeliveries { get; set; }
    }

    public class OrderingHourDto
    {
        public int Hour { get; set; }

        public string Label { get; set; } = string.Empty;

        public int Orders { get; set; }
    }

    public class ItemTimingDto
    {
        public int ItemId { get; set; }

        public string Name { get; set; } = string.Empty;

        public int CategoryId { get; set; }

        public string CategoryName { get; set; } = string.Empty;

        public List<AnalyticsTagDto> Tags { get; set; } = new();

        public int Deliveries { get; set; }

        public decimal AverageMinutes { get; set; }

        public decimal MedianMinutes { get; set; }

        /// <summary>1 = slowest.</summary>
        public int Rank { get; set; }
    }
}