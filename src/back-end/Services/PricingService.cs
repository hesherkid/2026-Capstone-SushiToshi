using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Helpers;
using Microsoft.EntityFrameworkCore;

namespace back_end.Services
{
    public class PricingService : IPricingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PricingService> _logger;

        public PricingService(ApplicationDbContext context, ILogger<PricingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public Task<PricingInfo> GetCurrentPricingAsync(int locationId, CancellationToken ct = default)
        {
            return GetPricingAsync(locationId, DateTime.UtcNow, ct);
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

        public async Task<PricingInfo> GetPricingAsync(int locationId, DateTime timestamp, CancellationToken ct = default)
        {
            var profile = await GetEffectiveProfileAsync(locationId, ct);
            var date = ToRestaurantTime(timestamp);

            // Holiday first, then weekend, then weekday
            var holidayName = profile.Apply_Holiday_Pricing
                ? await HolidayNameAsync(locationId, DateOnly.FromDateTime(date), ct)
                : null;

            var dayType = holidayName != null ? PricingDayType.Holiday
                : profile.Weekend_Days.Contains(date.DayOfWeek) ? PricingDayType.Weekend
                : PricingDayType.Weekday;

            var rate = profile.Rates.FirstOrDefault(r => r.Day_Type == dayType)
                ?? throw new PricingNotConfiguredException(
                    $"Pricing profile {profile.Pricing_Profile_Id} has no {dayType} prices.");

            var source = profile.Location_Id == null ? "Default" : "Location";
            _logger.LogInformation("Using {DayType} pricing ({Source}) for location {LocationId} on {Date:yyyy-MM-dd}",
                dayType, source, locationId, date);

            return new PricingInfo
            {
                PricingType = dayType.ToString(),
                PricingSource = source,
                HolidayName = holidayName,
                AdultBasePrice = rate.Adult_Price,
                SeniorBasePrice = rate.Senior_Price,
                ChildBasePrice = rate.Child_Price,
                ToddlerBasePrice = rate.Toddler_Price,
                TaxRate = profile.Tax_Rate,
                GratuityRate = profile.Gratuity_Rate,
                GratuityMinPartySize = profile.Gratuity_Min_Party_Size,
                LeftoverFee = profile.Leftover_Fee,
            };
        }

        public async Task<PricingProfile> GetEffectiveProfileAsync(int locationId, CancellationToken ct = default)
        {
            // The location's own profile and the default (location_id NULL) in one query
            var profiles = await _context.PricingProfiles
                .AsNoTracking()
                .Include(p => p.Rates)
                .Where(p => p.Location_Id == locationId || p.Location_Id == null)
                .ToListAsync(ct);

            return profiles.FirstOrDefault(p => p.Location_Id == locationId)
                ?? profiles.FirstOrDefault(p => p.Location_Id == null)
                ?? throw new PricingNotConfiguredException("There is no default pricing profile.");
        }

        // Called at startup right after migrations: without default prices every bill would be wrong,
        // so fail now rather than on the first bill
        public static async Task EnsureDefaultProfileAsync(ApplicationDbContext context, CancellationToken ct = default)
        {
            var dayTypes = await context.PricingRates
                .Where(r => r.PricingProfile.Location_Id == null)
                .Select(r => r.Day_Type)
                .ToListAsync(ct);

            var missing = Enum.GetValues<PricingDayType>().Except(dayTypes).ToList();
            if (missing.Count > 0)
            {
                throw new PricingNotConfiguredException(
                    $"Default pricing is missing {string.Join(", ", missing)} prices. " +
                    "Check the LocationPricingAndHolidays migration was applied.");
            }
        }

        // The approved holiday in the location's province on this date, or null
        private Task<string?> HolidayNameAsync(int locationId, DateOnly date, CancellationToken ct) =>
            _context.Locations
                .Where(l => l.Location_Id == locationId)
                .Join(_context.Holidays,
                    l => l.Province_Code,
                    h => h.Province_Code,
                    (l, h) => h)
                .Where(h => h.Date == date && h.Status == HolidayStatus.Approved)
                .Select(h => (string?)h.Name)
                .FirstOrDefaultAsync(ct);
    }
}
