
namespace back_end.Dtos.Analytics
{
    // Shared by the analytics reports (date range, location, tags, filter dropdowns).

    public class AnalyticsRangeDto
    {
        public string Preset { get; set; } = string.Empty;

        public string From { get; set; } = string.Empty;

        public string To { get; set; } = string.Empty;

        public int Days { get; set; }

        public string TimeZone { get; set; } = string.Empty;
    }

    public class AnalyticsLocationDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    public class AnalyticsTagDto
    {
        public int TagId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Color { get; set; } = string.Empty;
    }

    public class AnalyticsFiltersDto
    {
        public List<AnalyticsFilterOptionDto> Categories { get; set; } = new();

        public List<AnalyticsTagDto> Tags { get; set; } = new();
    }

    public class AnalyticsFilterOptionDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    public class DurationSummaryDto
    {
        public int Count { get; set; }

        public decimal? AverageMinutes { get; set; }

        public decimal? MedianMinutes { get; set; }
    }
}