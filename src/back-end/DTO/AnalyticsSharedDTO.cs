using System.Text.Json.Serialization;

namespace back_end.DTO.Analytics
{
    // Shared by the analytics reports (date range, location, tags, filter dropdowns).

    public class AnalyticsRangeDTO
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

    public class AnalyticsLocationDTO
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class AnalyticsTagDTO
    {
        [JsonPropertyName("tag_id")]
        public int TagId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("color")]
        public string Color { get; set; } = string.Empty;
    }

    public class AnalyticsFiltersDTO
    {
        [JsonPropertyName("categories")]
        public List<AnalyticsFilterOptionDTO> Categories { get; set; } = new();

        [JsonPropertyName("tags")]
        public List<AnalyticsTagDTO> Tags { get; set; } = new();
    }

    public class AnalyticsFilterOptionDTO
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}