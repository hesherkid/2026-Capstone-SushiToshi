using back_end.domain.Entities;

namespace back_end.Services
{
    public interface IPricingService
    {
        // Prices for a location at a moment in time (weekday, weekend or holiday in Edmonton time)
        Task<PricingInfo> GetPricingAsync(int locationId, DateTime timestamp, CancellationToken ct = default);
        Task<PricingInfo> GetCurrentPricingAsync(int locationId, CancellationToken ct = default);

        // The location's own profile if it has one, otherwise the default profile
        Task<PricingProfile> GetEffectiveProfileAsync(int locationId, CancellationToken ct = default);
    }

    public class PricingInfo
    {
        public string PricingType { get; set; } = string.Empty;   // "Weekday", "Weekend" or "Holiday"

        // "Location" when the location has its own prices, "Default" otherwise
        public string PricingSource { get; set; } = string.Empty;

        // e.g. "Canada Day"; null unless PricingType is "Holiday"
        public string? HolidayName { get; set; }

        public decimal AdultBasePrice { get; set; }
        public decimal SeniorBasePrice { get; set; }
        public decimal ChildBasePrice { get; set; }
        public decimal ToddlerBasePrice { get; set; }
        // GST (Alberta has no provincial sales tax)
        public decimal TaxRate { get; set; }

        // Automatic gratuity once the table has this many guests; 0 = never
        public decimal GratuityRate { get; set; }
        public int GratuityMinPartySize { get; set; }

        public decimal LeftoverFee { get; set; }
    }
}
