using System.ComponentModel.DataAnnotations;
using back_end.domain.enums;

namespace back_end.Dtos
{
    public class PricingRateDto
    {
        public PricingDayType DayType { get; set; }

        [Range(typeof(decimal), "0", "1000")]
        public decimal AdultPrice { get; set; }

        [Range(typeof(decimal), "0", "1000")]
        public decimal SeniorPrice { get; set; }

        [Range(typeof(decimal), "0", "1000")]
        public decimal ChildPrice { get; set; }

        [Range(typeof(decimal), "0", "1000")]
        public decimal ToddlerPrice { get; set; }
    }

    // Body for PUT /api/pricing/default and PUT /api/pricing/locations/{id}
    public class PricingProfileRequest : IValidatableObject
    {
        // 0.05 = 5% GST
        [Range(typeof(decimal), "0", "1")]
        public decimal TaxRate { get; set; }

        [Range(typeof(decimal), "0", "1")]
        public decimal GratuityRate { get; set; }

        // 0 = never add a gratuity
        [Range(0, 100)]
        public int GratuityMinPartySize { get; set; }

        [Range(typeof(decimal), "0", "1000")]
        public decimal LeftoverFee { get; set; }

        // e.g. ["Friday", "Saturday", "Sunday"]
        public List<DayOfWeek> WeekendDays { get; set; } = new();

        public bool ApplyHolidayPricing { get; set; } = true;

        [Required]
        public List<PricingRateDto> Rates { get; set; } = new();

        // Exactly one rate for each day type, and real weekdays only
        // (the enum converter also accepts numbers, so 7 would otherwise get through)
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var dayTypes = Rates.Select(r => r.DayType).OrderBy(d => d);
            if (!dayTypes.SequenceEqual(Enum.GetValues<PricingDayType>()))
            {
                yield return new ValidationResult(
                  "Rates must have exactly one entry each for Weekday, Weekend and Holiday.", new[] { nameof(Rates) });
            }

            if (WeekendDays.Any(d => !Enum.IsDefined(d)) || WeekendDays.Distinct().Count() != WeekendDays.Count)
            {
                yield return new ValidationResult(
                  "Weekend days must be different days of the week.", new[] { nameof(WeekendDays) });
            }
        }
    }

    public class PricingProfileResponse
    {
        public int PricingProfileId { get; set; }

        // null for the default profile
        public int? LocationId { get; set; }

        // "Default" or "Location"
        public string Source { get; set; } = string.Empty;

        public decimal TaxRate { get; set; }

        public decimal GratuityRate { get; set; }

        public int GratuityMinPartySize { get; set; }

        public decimal LeftoverFee { get; set; }

        public List<DayOfWeek> WeekendDays { get; set; } = new();

        public bool ApplyHolidayPricing { get; set; }

        public List<PricingRateDto> Rates { get; set; } = new();

        public DateTime UpdatedAt { get; set; }
    }

    public class LocationPricingSummary
    {
        public int LocationId { get; set; }

        public string LocationName { get; set; } = string.Empty;

        public string ProvinceCode { get; set; } = string.Empty;

        // "Default" or "Location"
        public string Source { get; set; } = string.Empty;
    }

    // What a bill created on this date at this location would be charged
    public class PricingPreviewResponse
    {
        public int LocationId { get; set; }

        public DateOnly Date { get; set; }

        public string PricingType { get; set; } = string.Empty;

        public string PricingSource { get; set; } = string.Empty;

        public string? HolidayName { get; set; }

        public decimal AdultPrice { get; set; }

        public decimal SeniorPrice { get; set; }

        public decimal ChildPrice { get; set; }

        public decimal ToddlerPrice { get; set; }

        public decimal TaxRate { get; set; }

        public decimal GratuityRate { get; set; }

        public int GratuityMinPartySize { get; set; }

        public decimal LeftoverFee { get; set; }
    }
}
