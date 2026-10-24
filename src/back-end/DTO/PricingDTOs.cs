using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using back_end.domain.enums;

namespace back_end.DTO.PricingDTOs
{
    public class PricingRateDTO
    {
        [JsonPropertyName("day_type")]
        public PricingDayType Day_Type { get; set; }

        [JsonPropertyName("adult_price")]
        [Range(typeof(decimal), "0", "1000")]
        public decimal Adult_Price { get; set; }

        [JsonPropertyName("senior_price")]
        [Range(typeof(decimal), "0", "1000")]
        public decimal Senior_Price { get; set; }

        [JsonPropertyName("child_price")]
        [Range(typeof(decimal), "0", "1000")]
        public decimal Child_Price { get; set; }

        [JsonPropertyName("toddler_price")]
        [Range(typeof(decimal), "0", "1000")]
        public decimal Toddler_Price { get; set; }
    }

    // Body for PUT /api/pricing/default and PUT /api/pricing/locations/{id}
    public class PricingProfileRequest : IValidatableObject
    {
        // 0.05 = 5% GST
        [JsonPropertyName("tax_rate")]
        [Range(typeof(decimal), "0", "1")]
        public decimal Tax_Rate { get; set; }

        [JsonPropertyName("gratuity_rate")]
        [Range(typeof(decimal), "0", "1")]
        public decimal Gratuity_Rate { get; set; }

        // 0 = never add a gratuity
        [JsonPropertyName("gratuity_min_party_size")]
        [Range(0, 100)]
        public int Gratuity_Min_Party_Size { get; set; }

        [JsonPropertyName("leftover_fee")]
        [Range(typeof(decimal), "0", "1000")]
        public decimal Leftover_Fee { get; set; }

        // e.g. ["Friday", "Saturday", "Sunday"]
        [JsonPropertyName("weekend_days")]
        public List<DayOfWeek> Weekend_Days { get; set; } = new();

        [JsonPropertyName("apply_holiday_pricing")]
        public bool Apply_Holiday_Pricing { get; set; } = true;

        [JsonPropertyName("rates")]
        [Required]
        public List<PricingRateDTO> Rates { get; set; } = new();

        // Exactly one rate for each day type, and real weekdays only
        // (the enum converter also accepts numbers, so 7 would otherwise get through)
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var dayTypes = Rates.Select(r => r.Day_Type).OrderBy(d => d);
            if (!dayTypes.SequenceEqual(Enum.GetValues<PricingDayType>()))
            {
                yield return new ValidationResult(
                  "Rates must have exactly one entry each for Weekday, Weekend and Holiday.", new[] { nameof(Rates) });
            }

            if (Weekend_Days.Any(d => !Enum.IsDefined(d)) || Weekend_Days.Distinct().Count() != Weekend_Days.Count)
            {
                yield return new ValidationResult(
                  "Weekend days must be different days of the week.", new[] { nameof(Weekend_Days) });
            }
        }
    }

    public class PricingProfileResponse
    {
        [JsonPropertyName("pricing_profile_id")]
        public int Pricing_Profile_Id { get; set; }

        // null for the default profile
        [JsonPropertyName("location_id")]
        public int? Location_Id { get; set; }

        // "Default" or "Location"
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("tax_rate")]
        public decimal Tax_Rate { get; set; }

        [JsonPropertyName("gratuity_rate")]
        public decimal Gratuity_Rate { get; set; }

        [JsonPropertyName("gratuity_min_party_size")]
        public int Gratuity_Min_Party_Size { get; set; }

        [JsonPropertyName("leftover_fee")]
        public decimal Leftover_Fee { get; set; }

        [JsonPropertyName("weekend_days")]
        public List<DayOfWeek> Weekend_Days { get; set; } = new();

        [JsonPropertyName("apply_holiday_pricing")]
        public bool Apply_Holiday_Pricing { get; set; }

        [JsonPropertyName("rates")]
        public List<PricingRateDTO> Rates { get; set; } = new();

        [JsonPropertyName("updated_at")]
        public DateTime Updated_At { get; set; }
    }

    public class LocationPricingSummary
    {
        [JsonPropertyName("location_id")]
        public int Location_Id { get; set; }

        [JsonPropertyName("location_name")]
        public string Location_Name { get; set; } = string.Empty;

        [JsonPropertyName("province_code")]
        public string Province_Code { get; set; } = string.Empty;

        // "Default" or "Location"
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;
    }

    // What a bill created on this date at this location would be charged
    public class PricingPreviewResponse
    {
        [JsonPropertyName("location_id")]
        public int Location_Id { get; set; }

        [JsonPropertyName("date")]
        public DateOnly Date { get; set; }

        [JsonPropertyName("pricing_type")]
        public string Pricing_Type { get; set; } = string.Empty;

        [JsonPropertyName("pricing_source")]
        public string Pricing_Source { get; set; } = string.Empty;

        [JsonPropertyName("holiday_name")]
        public string? Holiday_Name { get; set; }

        [JsonPropertyName("adult_price")]
        public decimal Adult_Price { get; set; }

        [JsonPropertyName("senior_price")]
        public decimal Senior_Price { get; set; }

        [JsonPropertyName("child_price")]
        public decimal Child_Price { get; set; }

        [JsonPropertyName("toddler_price")]
        public decimal Toddler_Price { get; set; }

        [JsonPropertyName("tax_rate")]
        public decimal Tax_Rate { get; set; }

        [JsonPropertyName("gratuity_rate")]
        public decimal Gratuity_Rate { get; set; }

        [JsonPropertyName("gratuity_min_party_size")]
        public int Gratuity_Min_Party_Size { get; set; }

        [JsonPropertyName("leftover_fee")]
        public decimal Leftover_Fee { get; set; }
    }
}
