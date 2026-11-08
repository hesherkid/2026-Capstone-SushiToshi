using System.Text.Json.Serialization;

namespace back_end.DTO.Analytics
{
    // Shared by the analytics reports (date range, location, tags, filter dropdowns).

    public class AnalyticsRangeDto
    {
        [JsonPropertyName("preset")]
        public string Preset { get; set; } = string.Empty;

        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty;

        [JsonPropertyName("to")]
        public string To { get; set; } = string.Empty;

        [JsonPropertyName("days")]
        public int Days { get; set; }

        [JsonPropertyName("time_zone")]
        public string TimeZone { get; set; } = string.Empty;
    }

    public class AnalyticsLocationDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class AnalyticsTagDto
    {
        [JsonPropertyName("tag_id")]
        public int TagId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("color")]
        public string Color { get; set; } = string.Empty;
    }

    public class AnalyticsFiltersDto
    {
        [JsonPropertyName("categories")]
        public List<AnalyticsFilterOptionDto> Categories { get; set; } = new();

        [JsonPropertyName("tags")]
        public List<AnalyticsTagDto> Tags { get; set; } = new();
    }

    public class AnalyticsFilterOptionDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class DurationSummaryDto
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("average_minutes")]
        public decimal? AverageMinutes { get; set; }

        [JsonPropertyName("median_minutes")]
        public decimal? MedianMinutes { get; set; }
    }
}