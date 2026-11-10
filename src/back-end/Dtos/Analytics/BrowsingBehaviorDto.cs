
namespace back_end.Dtos.Analytics
{
    public class BrowsingBehaviorResponseDto
    {
        public AnalyticsRangeDto Range { get; set; } = new();

        public AnalyticsLocationDto? Location { get; set; }

        public BrowsingSummaryDto Summary { get; set; } = new();

        public List<BrowsingItemDto> MostViewed { get; set; } = new();

        public List<BrowsingItemDto> LeastViewed { get; set; } = new();

        public List<BrowsingItemDto> NeverViewed { get; set; } = new();

        public List<BrowsingItemDto> ViewedNotOrdered { get; set; } = new();

        public List<BrowsingItemDto> TopViewedNotOrdered { get; set; } = new();

        public List<BrowsingItemDto> TopViewedAndOrdered { get; set; } = new();

        public List<BrowsingItemDto> LowConversionHighViews { get; set; } = new();

        public List<BrowsingItemDto> UnavailableViews { get; set; } = new();

        public List<BrowsingItemDto> Items { get; set; } = new();

        public AnalyticsFiltersDto Filters { get; set; } = new();
    }

    public class BrowsingSummaryDto
    {
        public int TotalViews { get; set; }

        public int ShortViewsExcluded { get; set; }

        public int TotalViewSeconds { get; set; }

        public decimal? AverageViewSeconds { get; set; }

        public decimal? AverageViewSecondsOrdered { get; set; }

        public decimal? AverageViewSecondsNotOrdered { get; set; }

        public int ViewingSessions { get; set; }

        public int SessionItemViews { get; set; }

        public int SessionItemOrders { get; set; }

        public decimal? ConversionRate { get; set; }

        public int ItemsConsidered { get; set; }

        public int ItemsViewed { get; set; }

        public int ItemsNeverViewed { get; set; }

        public int UnavailableViews { get; set; }

        public int MinViewSeconds { get; set; }

        public int MinViews { get; set; }
    }

    public class BrowsingItemDto
    {
        public int ItemId { get; set; }

        public string Name { get; set; } = string.Empty;

        public int CategoryId { get; set; }

        public string CategoryName { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public bool CurrentlyAvailable { get; set; }

        public List<AnalyticsTagDto> Tags { get; set; } = new();

        public int Views { get; set; }

        public int? ViewRank { get; set; }

        public int ShortViews { get; set; }

        public int TotalViewSeconds { get; set; }

        public decimal? AverageViewSeconds { get; set; }

        public int ViewingSessions { get; set; }

        public int OrderingSessions { get; set; }

        public decimal? ConversionRate { get; set; }

        public int UnitsOrderedAfterView { get; set; }

        public decimal? AverageViewSecondsOrdered { get; set; }

        public decimal? AverageViewSecondsNotOrdered { get; set; }

        public int UnavailableViews { get; set; }
    }
}