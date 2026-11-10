
namespace back_end.Dtos.Analytics
{

    public class ItemPerformanceResponseDto
    {
        public AnalyticsRangeDto Range { get; set; } = new();

        public AnalyticsLocationDto? Location { get; set; }

        public ItemPerformanceSummaryDto Summary { get; set; } = new();

        public List<ItemPerformanceDto> Best { get; set; } = new();

        public List<ItemPerformanceDto> Worst { get; set; } = new();

        public List<ItemPerformanceDto> ZeroSales { get; set; } = new();

        public List<ItemPerformanceDto> AddOns { get; set; } = new();

        public List<ItemPerformanceDto> Items { get; set; } = new();

        public AnalyticsFiltersDto Filters { get; set; } = new();
    }

    public class ItemPerformanceSummaryDto
    {
        public int TotalUnitsSold { get; set; }

        public int TotalAddOnUnits { get; set; }

        public int ItemsConsidered { get; set; }

        public int ItemsWithSales { get; set; }

        public int ItemsWithZeroSales { get; set; }
    }

    public class ItemPerformanceDto
    {
        public int ItemId { get; set; }

        public string Name { get; set; } = string.Empty;

        public int CategoryId { get; set; }

        public string CategoryName { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public bool CurrentlyAvailable { get; set; }

        public List<AnalyticsTagDto> Tags { get; set; } = new();

        public int UnitsSold { get; set; }

        public decimal ShareOfUnits { get; set; }

        public int AddOnUnits { get; set; }

        public decimal AddOnShare { get; set; }

        public int AddOnDays { get; set; }

        public int? Rank { get; set; }
    }
}