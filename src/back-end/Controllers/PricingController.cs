using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.DTO.PricingDTOs;
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
                  Location_Id = l.Location_Id,
                  Location_Name = l.Name,
                  Province_Code = l.Province_Code,
                  Source = l.PricingProfile != null ? "Location" : "Default",
              })
              .ToListAsync();
            return Ok(locations);
        }

        /// <summary>
        /// The pricing a location uses: its own if it has one, otherwise the default ("source" says which).
        /// </summary>
        [HttpGet("locations/{location_id}")]
        [Authorize(Policy = "adminOnly")]
        [ProducesResponseType(typeof(PricingProfileResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetLocation(int location_id)
        {
            if (!await LocationExistsAsync(location_id))
            {
                return NotFound(new { message = "Location was not found" });
            }
            return Ok(ToResponse(await _pricingService.GetEffectiveProfileAsync(location_id)));
        }

        /// <summary>
        /// Gives a location its own pricing, or replaces it. Same body as PUT /api/pricing/default.
        /// </summary>
        [HttpPut("locations/{location_id}")]
        [Authorize(Policy = "adminOnly")]
        [ProducesResponseType(typeof(PricingProfileResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateLocation(int location_id, PricingProfileRequest request)
        {
            try
            {
                if (!await LocationExistsAsync(location_id))
                {
                    return NotFound(new { message = "Location was not found" });
                }

                var profile = await LoadProfileAsync(location_id);
                if (profile == null)
                {
                    profile = new PricingProfile { Location_Id = location_id };
                    _context.PricingProfiles.Add(profile);
                }

                Apply(profile, request);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Location {LocationId} pricing updated", location_id);
                return Ok(ToResponse(profile));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating pricing for location {LocationId}", location_id);
                return StatusCode(500, new { message = "Error updating location pricing" });
            }
        }

        /// <summary>
        /// Removes a location's own pricing so it uses the default again. Returns the default pricing.
        /// </summary>
        [HttpDelete("locations/{location_id}")]
        [Authorize(Policy = "adminOnly")]
        [ProducesResponseType(typeof(PricingProfileResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteLocation(int location_id)
        {
            var profile = await LoadProfileAsync(location_id);
            if (profile == null)
            {
                return NotFound(new { message = "This location already uses the default pricing." });
            }

            _context.PricingProfiles.Remove(profile);   // its rates are removed by the cascade
            await _context.SaveChangesAsync();

            _logger.LogInformation("Location {LocationId} pricing removed; using default", location_id);
            return Ok(ToResponse(await _pricingService.GetEffectiveProfileAsync(location_id)));
        }

        /// <summary>
        /// The prices a bill created on this date at this location would get (today if no date).
        /// </summary>
        /// <remarks>
        ///     GET /api/pricing/locations/1/preview?date=2026-12-25
        /// </remarks>
        [HttpGet("locations/{location_id}/preview")]
        [Authorize(Policy = "staffOnly")]
        [ProducesResponseType(typeof(PricingPreviewResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Preview(int location_id, [FromQuery] DateOnly? date)
        {
            if (!await LocationExistsAsync(location_id))
            {
                return NotFound(new { message = "Location was not found" });
            }

            // Noon in Edmonton on that date, so converting to UTC can't move it to another day
            var day = date ?? DateOnly.FromDateTime(PricingService.ToRestaurantTime(DateTime.UtcNow));
            var noonUtc = TimeZoneInfo.ConvertTimeToUtc(
              day.ToDateTime(new TimeOnly(12, 0)), AnalyticsDateRange.RestaurantTimeZone);

            var pricing = await _pricingService.GetPricingAsync(location_id, noonUtc);
            return Ok(new PricingPreviewResponse
            {
                Location_Id = location_id,
                Date = day,
                Pricing_Type = pricing.PricingType,
                Pricing_Source = pricing.PricingSource,
                Holiday_Name = pricing.HolidayName,
                Adult_Price = pricing.AdultBasePrice,
                Senior_Price = pricing.SeniorBasePrice,
                Child_Price = pricing.ChildBasePrice,
                Toddler_Price = pricing.ToddlerBasePrice,
                Tax_Rate = pricing.TaxRate,
                Gratuity_Rate = pricing.GratuityRate,
                Gratuity_Min_Party_Size = pricing.GratuityMinPartySize,
                Leftover_Fee = pricing.LeftoverFee,
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
            profile.Tax_Rate = Math.Round(request.Tax_Rate, 4);
            profile.Gratuity_Rate = Math.Round(request.Gratuity_Rate, 4);
            profile.Gratuity_Min_Party_Size = request.Gratuity_Min_Party_Size;
            profile.Leftover_Fee = Math.Round(request.Leftover_Fee, 2);
            profile.Apply_Holiday_Pricing = request.Apply_Holiday_Pricing;

            // Monday first, so the stored value reads naturally
            profile.Weekend_Days = request.Weekend_Days.OrderBy(d => ((int)d + 6) % 7).ToList();

            // Update rates in place so each day type keeps its row
            foreach (var requested in request.Rates)
            {
                var rate = profile.Rates.FirstOrDefault(r => r.Day_Type == requested.Day_Type);
                if (rate == null)
                {
                    rate = new PricingRate { Day_Type = requested.Day_Type };
                    profile.Rates.Add(rate);
                }
                rate.Adult_Price = Math.Round(requested.Adult_Price, 2);
                rate.Senior_Price = Math.Round(requested.Senior_Price, 2);
                rate.Child_Price = Math.Round(requested.Child_Price, 2);
                rate.Toddler_Price = Math.Round(requested.Toddler_Price, 2);
            }

            profile.Updated_At = DateTime.UtcNow;
        }

        private static PricingProfileResponse ToResponse(PricingProfile profile) => new()
        {
            Pricing_Profile_Id = profile.Pricing_Profile_Id,
            Location_Id = profile.Location_Id,
            Source = profile.Location_Id == null ? "Default" : "Location",
            Tax_Rate = profile.Tax_Rate,
            Gratuity_Rate = profile.Gratuity_Rate,
            Gratuity_Min_Party_Size = profile.Gratuity_Min_Party_Size,
            Leftover_Fee = profile.Leftover_Fee,
            Weekend_Days = profile.Weekend_Days,
            Apply_Holiday_Pricing = profile.Apply_Holiday_Pricing,
            Updated_At = profile.Updated_At,
            Rates = profile.Rates
            .OrderBy(r => r.Day_Type)
            .Select(r => new PricingRateDTO
            {
                Day_Type = r.Day_Type,
                Adult_Price = r.Adult_Price,
                Senior_Price = r.Senior_Price,
                Child_Price = r.Child_Price,
                Toddler_Price = r.Toddler_Price,
            })
            .ToList(),
        };
    }
}
