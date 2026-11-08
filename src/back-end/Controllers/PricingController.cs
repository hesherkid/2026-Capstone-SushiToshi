using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.Dtos;
using back_end.Helpers;
using back_end.Services;

namespace back_end.Controllers
{
    [ApiController]
    [Route("api/pricing")]
    // Policies are set per action: adminOnly on the class would also lock staff out of the preview
    [Authorize]
    public class PricingController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPricingService _pricingService;
        private readonly ILogger<PricingController> _logger;

        public PricingController(ApplicationDbContext context, IPricingService pricingService, ILogger<PricingController> logger)
        {
            _context = context;
            _pricingService = pricingService;
            _logger = logger;
        }

        /// <summary>
        /// The default pricing, used by every location without its own.
        /// </summary>
        [HttpGet("default")]
        [Authorize(Policy = "adminOnly")]
        [ProducesResponseType(typeof(PricingProfileResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetDefault()
        {
            var profile = await LoadProfileAsync(null);
            if (profile == null)
            {
                return NotFound(new { message = "There is no default pricing profile." });
            }
            return Ok(ToResponse(profile));
        }

        /// <summary>
        /// Updates the default pricing.
        /// </summary>
        /// <remarks>
        /// Sample request:
        ///
        ///     PUT /api/pricing/default
        ///     {
        ///       "tax_rate": 0.05, "gratuity_rate": 0.18, "gratuity_min_party_size": 6,
        ///       "leftover_fee": 15.00, "weekend_days": ["Friday", "Saturday", "Sunday"],
        ///       "apply_holiday_pricing": true,
        ///       "rates": [
        ///         { "day_type": "Weekday", "adult_price": 40.95, "senior_price": 35.95, "child_price": 25.95, "toddler_price": 7.95 },
        ///         { "day_type": "Weekend", "adult_price": 42.95, "senior_price": 39.95, "child_price": 28.95, "toddler_price": 7.95 },
        ///         { "day_type": "Holiday", "adult_price": 42.95, "senior_price": 39.95, "child_price": 28.95, "toddler_price": 7.95 }
        ///       ]
        ///     }
        ///
        /// Open bills pick up the new prices; closed bills keep the prices they closed with.
        /// </remarks>
        [HttpPut("default")]
        [Authorize(Policy = "adminOnly")]
        [ProducesResponseType(typeof(PricingProfileResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateDefault(PricingProfileRequest request)
        {
            try
            {
                var profile = await LoadProfileAsync(null);
                if (profile == null)
                {
                    return NotFound(new { message = "There is no default pricing profile." });
                }

                Apply(profile, request);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Default pricing updated");
                return Ok(ToResponse(profile));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating default pricing");
                return StatusCode(500, new { message = "Error updating default pricing" });
            }
        }

        /// <summary>
        /// Every location and whether it uses the default pricing or its own.
        /// </summary>
        [HttpGet("locations")]
        [Authorize(Policy = "adminOnly")]
        [ProducesResponseType(typeof(IEnumerable<LocationPricingSummary>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLocations()
        {
            var locations = await _context.Locations
              .OrderBy(l => l.Location_Id)
              .Select(l => new LocationPricingSummary
              {
                  LocationId = l.Location_Id,
                  LocationName = l.Name,
                  ProvinceCode = l.Province_Code,
                  Source = l.PricingProfile != null ? "Location" : "Default",
              })
              .ToListAsync();
            return Ok(locations);
        }

        /// <summary>
        /// The pricing a location uses: its own if it has one, otherwise the default ("source" says which).
        /// </summary>
        [HttpGet("locations/{locationId}")]
        [Authorize(Policy = "adminOnly")]
        [ProducesResponseType(typeof(PricingProfileResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetLocation(int locationId)
        {
            if (!await LocationExistsAsync(locationId))
            {
                return NotFound(new { message = "Location was not found" });
            }
            return Ok(ToResponse(await _pricingService.GetEffectiveProfileAsync(locationId)));
        }

        /// <summary>
        /// Gives a location its own pricing, or replaces it. Same body as PUT /api/pricing/default.
        /// </summary>
        [HttpPut("locations/{locationId}")]
        [Authorize(Policy = "adminOnly")]
        [ProducesResponseType(typeof(PricingProfileResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateLocation(int locationId, PricingProfileRequest request)
        {
            try
            {
                if (!await LocationExistsAsync(locationId))
                {
                    return NotFound(new { message = "Location was not found" });
                }

                var profile = await LoadProfileAsync(locationId);
                if (profile == null)
                {
                    profile = new PricingProfile { Location_Id = locationId };
                    _context.PricingProfiles.Add(profile);
                }

                Apply(profile, request);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Location {LocationId} pricing updated", locationId);
                return Ok(ToResponse(profile));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating pricing for location {LocationId}", locationId);
                return StatusCode(500, new { message = "Error updating location pricing" });
            }
        }

        /// <summary>
        /// Removes a location's own pricing so it uses the default again. Returns the default pricing.
        /// </summary>
        [HttpDelete("locations/{locationId}")]
        [Authorize(Policy = "adminOnly")]
        [ProducesResponseType(typeof(PricingProfileResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteLocation(int locationId)
        {
            var profile = await LoadProfileAsync(locationId);
            if (profile == null)
            {
                return NotFound(new { message = "This location already uses the default pricing." });
            }

            _context.PricingProfiles.Remove(profile);   // its rates are removed by the cascade
            await _context.SaveChangesAsync();

            _logger.LogInformation("Location {LocationId} pricing removed; using default", locationId);
            return Ok(ToResponse(await _pricingService.GetEffectiveProfileAsync(locationId)));
        }

        /// <summary>
        /// The prices a bill created on this date at this location would get (today if no date).
        /// </summary>
        /// <remarks>
        ///     GET /api/pricing/locations/1/preview?date=2026-12-25
        /// </remarks>
        [HttpGet("locations/{locationId}/preview")]
        [Authorize(Policy = "staffOnly")]
        [ProducesResponseType(typeof(PricingPreviewResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Preview(int locationId, [FromQuery] DateOnly? date)
        {
            if (!await LocationExistsAsync(locationId))
            {
                return NotFound(new { message = "Location was not found" });
            }

            // Noon in Edmonton on that date, so converting to UTC can't move it to another day
            var day = date ?? DateOnly.FromDateTime(PricingService.ToRestaurantTime(DateTime.UtcNow));
            var noonUtc = TimeZoneInfo.ConvertTimeToUtc(
              day.ToDateTime(new TimeOnly(12, 0)), AnalyticsDateRange.RestaurantTimeZone);

            var pricing = await _pricingService.GetPricingAsync(locationId, noonUtc);
            return Ok(new PricingPreviewResponse
            {
                LocationId = locationId,
                Date = day,
                PricingType = pricing.PricingType,
                PricingSource = pricing.PricingSource,
                HolidayName = pricing.HolidayName,
                AdultPrice = pricing.AdultBasePrice,
                SeniorPrice = pricing.SeniorBasePrice,
                ChildPrice = pricing.ChildBasePrice,
                ToddlerPrice = pricing.ToddlerBasePrice,
                TaxRate = pricing.TaxRate,
                GratuityRate = pricing.GratuityRate,
                GratuityMinPartySize = pricing.GratuityMinPartySize,
                LeftoverFee = pricing.LeftoverFee,
            });
        }

        // NULL = the default profile
        private Task<PricingProfile?> LoadProfileAsync(int? locationId) =>
          _context.PricingProfiles
            .Include(p => p.Rates)
            .FirstOrDefaultAsync(p => p.Location_Id == locationId);

        private Task<bool> LocationExistsAsync(int locationId) =>
          _context.Locations.AnyAsync(l => l.Location_Id == locationId);

        private static void Apply(PricingProfile profile, PricingProfileRequest request)
        {
            profile.Tax_Rate = Math.Round(request.TaxRate, 4);
            profile.Gratuity_Rate = Math.Round(request.GratuityRate, 4);
            profile.Gratuity_Min_Party_Size = request.GratuityMinPartySize;
            profile.Leftover_Fee = Math.Round(request.LeftoverFee, 2);
            profile.Apply_Holiday_Pricing = request.ApplyHolidayPricing;

            // Monday first, so the stored value reads naturally
            profile.Weekend_Days = request.WeekendDays.OrderBy(d => ((int)d + 6) % 7).ToList();

            // Update rates in place so each day type keeps its row
            foreach (var requested in request.Rates)
            {
                var rate = profile.Rates.FirstOrDefault(r => r.Day_Type == requested.DayType);
                if (rate == null)
                {
                    rate = new PricingRate { Day_Type = requested.DayType };
                    profile.Rates.Add(rate);
                }
                rate.Adult_Price = Math.Round(requested.AdultPrice, 2);
                rate.Senior_Price = Math.Round(requested.SeniorPrice, 2);
                rate.Child_Price = Math.Round(requested.ChildPrice, 2);
                rate.Toddler_Price = Math.Round(requested.ToddlerPrice, 2);
            }

            profile.Updated_At = DateTime.UtcNow;
        }

        private static PricingProfileResponse ToResponse(PricingProfile profile) => new()
        {
            PricingProfileId = profile.Pricing_Profile_Id,
            LocationId = profile.Location_Id,
            Source = profile.Location_Id == null ? "Default" : "Location",
            TaxRate = profile.Tax_Rate,
            GratuityRate = profile.Gratuity_Rate,
            GratuityMinPartySize = profile.Gratuity_Min_Party_Size,
            LeftoverFee = profile.Leftover_Fee,
            WeekendDays = profile.Weekend_Days,
            ApplyHolidayPricing = profile.Apply_Holiday_Pricing,
            UpdatedAt = profile.Updated_At,
            Rates = profile.Rates
            .OrderBy(r => r.Day_Type)
            .Select(r => new PricingRateDto
            {
                DayType = r.Day_Type,
                AdultPrice = r.Adult_Price,
                SeniorPrice = r.Senior_Price,
                ChildPrice = r.Child_Price,
                ToddlerPrice = r.Toddler_Price,
            })
            .ToList(),
        };
    }
}
