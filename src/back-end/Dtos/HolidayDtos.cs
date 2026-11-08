using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using back_end.domain.enums;

namespace back_end.Dtos
{
    public class HolidayResponse
    {
        [JsonPropertyName("holiday_id")]
        public int HolidayId { get; set; }

        [JsonPropertyName("province_code")]
        public string ProvinceCode { get; set; } = string.Empty;

        [JsonPropertyName("date")]
        public DateOnly Date { get; set; }

        [JsonPropertyName("observed_date")]
        public DateOnly? ObservedDate { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public HolidaySource Source { get; set; }

        [JsonPropertyName("status")]
        public HolidayStatus Status { get; set; }

        [JsonPropertyName("imported_at")]
        public DateTime? ImportedAt { get; set; }

        [JsonPropertyName("reviewed_by")]
        public int? ReviewedBy { get; set; }

        [JsonPropertyName("reviewed_at")]
        public DateTime? ReviewedAt { get; set; }
    }

    // Everything the Holidays page needs for its dropdowns
    public class HolidayOptionsResponse
    {
        [JsonPropertyName("provinces")]
        public IReadOnlyList<string> Provinces { get; set; } = Array.Empty<string>();

        [JsonPropertyName("default_province")]
        public string DefaultProvince { get; set; } = string.Empty;

        [JsonPropertyName("year_from")]
        public int YearFrom { get; set; }

        [JsonPropertyName("year_to")]
        public int YearTo { get; set; }

        [JsonPropertyName("include_optional")]
        public bool IncludeOptional { get; set; }
    }

    public class HolidayImportRequest
    {
        [JsonPropertyName("province_code")]
        [Required]
        public string ProvinceCode { get; set; } = string.Empty;

        // Checked against HolidayApi:YearsBack/YearsAhead by the service
        [JsonPropertyName("year")]
        [Range(2000, 2100)]
        public int Year { get; set; }

        // null = use the HolidayApi:IncludeOptional setting
        [JsonPropertyName("include_optional")]
        public bool? IncludeOptional { get; set; }
    }

    public class HolidayImportResponse
    {
        [JsonPropertyName("added")]
        public int Added { get; set; }

        // Already stored (with any status), so left as they were
        [JsonPropertyName("skipped")]
        public int Skipped { get; set; }

        [JsonPropertyName("holidays")]
        public List<HolidayResponse> Holidays { get; set; } = new();
    }

    public class HolidayReviewRequest
    {
        [JsonPropertyName("ids")]
        [Required, MinLength(1), MaxLength(200)]
        public List<int> Ids { get; set; } = new();
    }

    public class HolidayReviewResponse
    {
        [JsonPropertyName("updated")]
        public int Updated { get; set; }
    }

    public class HolidayCreateRequest
    {
        [JsonPropertyName("province_code")]
        [Required]
        public string ProvinceCode { get; set; } = string.Empty;

        // Nullable so a missing date is a 400, not 0001-01-01
        [JsonPropertyName("date")]
        [Required]
        public DateOnly? Date { get; set; }

        [JsonPropertyName("name")]
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }

    public class HolidayUpdateRequest
    {
        [JsonPropertyName("date")]
        [Required]
        public DateOnly? Date { get; set; }

        [JsonPropertyName("name")]
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}
