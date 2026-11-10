using System.ComponentModel.DataAnnotations;
using back_end.domain.enums;

namespace back_end.Dtos
{
    public class HolidayResponse
    {
        public int HolidayId { get; set; }

        public string ProvinceCode { get; set; } = string.Empty;

        public DateOnly Date { get; set; }

        public DateOnly? ObservedDate { get; set; }

        public string Name { get; set; } = string.Empty;

        public HolidaySource Source { get; set; }

        public HolidayStatus Status { get; set; }

        public DateTime? ImportedAt { get; set; }

        public int? ReviewedBy { get; set; }

        public DateTime? ReviewedAt { get; set; }
    }

    // Everything the Holidays page needs for its dropdowns
    public class HolidayOptionsResponse
    {
        public IReadOnlyList<string> Provinces { get; set; } = Array.Empty<string>();

        public string DefaultProvince { get; set; } = string.Empty;

        public int YearFrom { get; set; }

        public int YearTo { get; set; }

        public bool IncludeOptional { get; set; }
    }

    public class HolidayImportRequest
    {
        [Required]
        public string ProvinceCode { get; set; } = string.Empty;

        // Checked against HolidayApi:YearsBack/YearsAhead by the service
        [Range(2000, 2100)]
        public int Year { get; set; }

        // null = use the HolidayApi:IncludeOptional setting
        public bool? IncludeOptional { get; set; }
    }

    public class HolidayImportResponse
    {
        public int Added { get; set; }

        // Already stored (with any status), so left as they were
        public int Skipped { get; set; }

        public List<HolidayResponse> Holidays { get; set; } = new();
    }

    public class HolidayReviewRequest
    {
        [Required, MinLength(1), MaxLength(200)]
        public List<int> Ids { get; set; } = new();
    }

    public class HolidayReviewResponse
    {
        public int Updated { get; set; }
    }

    public class HolidayCreateRequest
    {
        [Required]
        public string ProvinceCode { get; set; } = string.Empty;

        // Nullable so a missing date is a 400, not 0001-01-01
        [Required]
        public DateOnly? Date { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }

    public class HolidayUpdateRequest
    {
        [Required]
        public DateOnly? Date { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}
