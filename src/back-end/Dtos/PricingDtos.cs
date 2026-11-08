using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using back_end.domain.enums;

namespace back_end.Dtos
{
    public class PricingRateDto
    {
        [JsonPropertyName("day_type")]
        public PricingDayType DayType { get; set; }

        [JsonPropertyName("adult_price")]
        [Range(typeof(decimal), "0", "1000")]
        public decimal AdultPrice { get; set; }

        [JsonPropertyName("senior_price")]
        [Range(typeof(decimal), "0", "1000")]
        public decimal SeniorPrice { get; set; }

        [JsonPropertyName("child_price")]
        [Range(typeof(decimal), "0", "1000")]
        public decimal ChildPrice { get; set; }

        [JsonPropertyName("toddler_price")]
        [Range(typeof(decimal), "0", "1000")]
        public decimal ToddlerPrice { get; set; }
    }

    // Body for PUT /api/pricing/default and PUT /api/pricing/locations/{id}
    public class PricingProfileRequest : IValidatableObject
    {
        // 0.05 = 5% GST
        [JsonPropertyName("tax_rate")]
        [Range(typeof(decimal), "0", "1")]
        public decimal TaxRate { get; set; }

        [JsonPropertyName("gratuity_rate")]
        [Range(typeof(decimal), "0", "1")]
        public decimal GratuityRate { get; set; }

        // 0 = never add a gratuity
        [JsonPropertyName("gratuity_min_party_size")]
        [Range(0, 100)]
        public int GratuityMinPartySize { get; set; }

        [JsonPropertyName("leftover_fee")]
        [Range(typeof(decimal), "0", "1000")]
        public decimal LeftoverFee { get; set; }

        // e.g. ["Friday", "Saturday", "Sunday"]
        [JsonPropertyName("weekend_days")]
        public List<DayOfWeek> WeekendDays { get; set; } = new();

        [JsonPropertyName("apply_holiday_pricing")]
        public bool ApplyHolidayPricing { get; set; } = true;

        [JsonPropertyName("rates")]
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
        [JsonPropertyName("pricing_profile_id")]
        public int PricingProfileId { get; set; }

        // null for the default profile
        [JsonPropertyName("location_id")]
        public int? LocationId { get; set; }

        // "Default" or "Location"
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("tax_rate")]
        public decimal TaxRate { get; set; }

        [JsonPropertyName("gratuity_rate")]
        public decimal GratuityRate { get; set; }

        [JsonPropertyName("gratuity_min_party_size")]
        public int GratuityMinPartySize { get; set; }

        [JsonPropertyName("leftover_fee")]
        public decimal LeftoverFee { get; set; }

        [JsonPropertyName("weekend_days")]
        public List<DayOfWeek> WeekendDays { get; set; } = new();

        [JsonPropertyName("apply_holiday_pricing")]
        public bool ApplyHolidayPricing { get; set; }

        [JsonPropertyName("rates")]
        public List<PricingRateDto> Rates { get; set; } = new();

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    public class LocationPricingSummary
    {
        [JsonPropertyName("location_id")]
        public int LocationId { get; set; }

        [JsonPropertyName("location_name")]
        public string LocationName { get; set; } = string.Empty;

        [JsonPropertyName("province_code")]
        public string ProvinceCode { get; set; } = string.Empty;

        // "Default" or "Location"
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;
    }

    // What a bill created on this date at this location would be charged
    public class PricingPreviewResponse
    {
        [JsonPropertyName("location_id")]
        public int LocationId { get; set; }

        [JsonPropertyName("date")]
        public DateOnly Date { get; set; }

        [JsonPropertyName("pricing_type")]
        public string PricingType { get; set; } = string.Empty;

        [JsonPropertyName("pricing_source")]
        public string PricingSource { get; set; } = string.Empty;

        [JsonPropertyName("holiday_name")]
        public string? HolidayName { get; set; }

        [JsonPropertyName("adult_price")]
        public decimal AdultPrice { get; set; }

        [JsonPropertyName("senior_price")]
        public decimal SeniorPrice { get; set; }

        [JsonPropertyName("child_price")]
        public decimal ChildPrice { get; set; }

        [JsonPropertyName("toddler_price")]
        public decimal ToddlerPrice { get; set; }

        [JsonPropertyName("tax_rate")]
        public decimal TaxRate { get; set; }

        [JsonPropertyName("gratuity_rate")]
        public decimal GratuityRate { get; set; }

        [JsonPropertyName("gratuity_min_party_size")]
        public int GratuityMinPartySize { get; set; }

        [JsonPropertyName("leftover_fee")]
        public decimal LeftoverFee { get; set; }
    }
}
