using back_end.Helpers;

namespace back_end.Services
{
    public class PricingService : IPricingService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<PricingService> _logger;

        public PricingService(IConfiguration config, ILogger<PricingService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public PricingInfo GetCurrentPricing()
        {
            return GetPricingForDate(DateTime.UtcNow);
        }

        // Timestamps are UTC, but weekday/weekend/holiday depends on the date in Edmonton.
        // A Thursday 7pm bill is already Friday in UTC, so without this it got weekend prices.
        public static DateTime ToRestaurantTime(DateTime timestamp)
        {
            var utc = timestamp.Kind switch
            {
                DateTimeKind.Local => timestamp.ToUniversalTime(),
                DateTimeKind.Utc => timestamp,
                _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc) // stored values are UTC
            };
            return TimeZoneInfo.ConvertTimeFromUtc(utc, AnalyticsDateRange.RestaurantTimeZone);
        }

        public PricingInfo GetPricingForDate(DateTime timestamp)
        {
            var date = ToRestaurantTime(timestamp);

            // Check if it's a holiday first
            var holidays = _config.GetSection("Pricing:Holiday:Dates").Get<List<string>>() ?? new List<string>();
            var dateStr = date.ToString("yyyy-MM-dd");
            
            if (holidays.Contains(dateStr))
            {
                _logger.LogInformation($"Using holiday pricing for {dateStr}");
                return new PricingInfo
                {
                    PricingType = "Holiday",
                    AdultBasePrice = _config.GetValue<decimal>("Pricing:Holiday:AdultBasePrice", 42.95m),
                    SeniorBasePrice = _config.GetValue<decimal>("Pricing:Holiday:SeniorBasePrice", 39.95m),
                    ChildBasePrice = _config.GetValue<decimal>("Pricing:Holiday:ChildBasePrice", 28.95m),
                    ToddlerBasePrice = _config.GetValue<decimal>("Pricing:Holiday:ToddlerBasePrice", 7.95m),
                    TaxRate = _config.GetValue<decimal>("Pricing:TaxRate", 0.13m)
                };
            }

            // Check if it's a weekend (Friday, Saturday, Sunday)
            var dayOfWeek = date.DayOfWeek.ToString();
            var weekendDays = _config.GetSection("Pricing:Weekend:Days").Get<List<string>>() ?? new List<string>();
            
            if (weekendDays.Contains(dayOfWeek))
            {
                _logger.LogInformation($"Using weekend pricing for {dayOfWeek}");
                return new PricingInfo
                {
                    PricingType = "Weekend",
                    AdultBasePrice = _config.GetValue<decimal>("Pricing:Weekend:AdultBasePrice", 42.95m),
                    SeniorBasePrice = _config.GetValue<decimal>("Pricing:Weekend:SeniorBasePrice", 39.95m),
                    ChildBasePrice = _config.GetValue<decimal>("Pricing:Weekend:ChildBasePrice", 28.95m),
                    ToddlerBasePrice = _config.GetValue<decimal>("Pricing:Weekend:ToddlerBasePrice", 7.95m),
                    TaxRate = _config.GetValue<decimal>("Pricing:TaxRate", 0.13m)
                };
            }

            // Default to weekday pricing
            _logger.LogInformation($"Using weekday pricing for {dayOfWeek}");
            return new PricingInfo
            {
                PricingType = "Weekday",
                AdultBasePrice = _config.GetValue<decimal>("Pricing:Weekday:AdultBasePrice", 40.95m),
                SeniorBasePrice = _config.GetValue<decimal>("Pricing:Weekday:SeniorBasePrice", 35.95m),
                ChildBasePrice = _config.GetValue<decimal>("Pricing:Weekday:ChildBasePrice", 25.95m),
                ToddlerBasePrice = _config.GetValue<decimal>("Pricing:Weekday:ToddlerBasePrice", 7.95m),
                TaxRate = _config.GetValue<decimal>("Pricing:TaxRate", 0.13m)
            };
        }
    }
}