using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.Dtos;
using back_end.Helpers;
using back_end.Services;

namespace back_end.Controllers
{
    // Staff Order History: past orders by date range, and which days have any
    [ApiController]
    [Route("api/order/history")]
    [Authorize(Policy = "staffOnly")]
    public class OrderHistoryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OrderHistoryController> _logger;
        private readonly ILocationAccessService _locationAccess;

        // Longest range staff can search in Order History
        public const int MaxHistoryRangeDays = 31;

        public OrderHistoryController(ApplicationDbContext context, ILogger<OrderHistoryController> logger, ILocationAccessService locationAccess)
        {
            _context = context;
            _logger = logger;
            _locationAccess = locationAccess;
        }

        /// <summary>
        /// Looks up past orders by date range.
        /// </summary>
        /// <param name="from">First restaurant-local day to include (yyyy-MM-dd)</param>
        /// <param name="to">Last restaurant-local day to include (yyyy-MM-dd), not in the future</param>
        /// <param name="locationId">Optional location filter. Staff always get their current location</param>
        /// <param name="tableNumber">Optional table number. Matches single tables and tables in a group</param>
        /// <param name="name">Optional guest or bill name. Partial, case-insensitive match</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/order/history?from=2026-10-20&amp;to=2026-10-26&amp;table_number=14&amp;name=lee
        ///
        /// </remarks>
        /// <response code="200">The orders placed in the range (can be empty).</response>
        /// <response code="400">from or to is missing or invalid, to is in the future, or the range is over 31 days.</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not staff, or a staff member with no working location.</response>
        /// <response code="500">The orders couldn't be loaded.</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<object>>> GetOrderHistory(
            [FromQuery] string? from,
            [FromQuery] string? to,
            [FromQuery(Name = "location_id")] int? locationId = null,
            [FromQuery(Name = "table_number")] int? tableNumber = null,
            [FromQuery] string? name = null)
        {
            try
            {
                var scope = await _locationAccess.ResolveLocationAsync(User, locationId);
                if (scope.IsForbidden) return LocationResults.NoLocation();
                locationId = scope.LocationId;

                if (!AnalyticsDateRange.TryResolve("custom", from, to, DateTime.UtcNow,
                        AnalyticsDateRange.RestaurantTimeZone, out var range, out var errorKey, out var errorMessage)
                    || range == null)
                {
                    return HistoryBadRequest(errorKey, errorMessage);
                }

                if (range.ToLocal.DayNumber - range.FromLocal.DayNumber + 1 > MaxHistoryRangeDays)
                {
                    return HistoryBadRequest("to", $"Choose a range of {MaxHistoryRangeDays} days or fewer.");
                }

                var query = _context.SessionOrders
                    .WithStaffOrderDetails()
                    .Where(o => o.Created_At >= range.StartUtc && o.Created_At < range.EndUtc);

                if (locationId.HasValue)
                {
                    query = query.Where(o => o.DiningSession.Location_Id == locationId.Value);
                }

                if (tableNumber.HasValue)
                {
                    var number = tableNumber.Value;
                    query = query.Where(o =>
                        (o.DiningSession.Table != null && o.DiningSession.Table.table_number == number) ||
                        (o.DiningSession.TableGroup != null && o.DiningSession.TableGroup.Tables.Any(t => t.table_number == number)));
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    var term = name.Trim().ToLower();
                    query = query.Where(o =>
                        o.Bill.Bill_Name.ToLower().Contains(term) ||
                        (o.User_Name != null && o.User_Name.ToLower().Contains(term)));
                }

                var orders = await query
                    .OrderByDescending(o => o.Created_At)
                    .ToListAsync();

                return Ok(orders.Select(o => o.ToStaffOrder()).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving order history");
                return StatusCode(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// Lists the days in a month that have at least one order, so the Order History
        /// calendar can mark them.
        /// </summary>
        /// <param name="month">The restaurant-local month to check (yyyy-MM)</param>
        /// <param name="locationId">Optional location filter. Staff always get their current location</param>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/order/history/days?month=2026-11&amp;location_id=1
        ///
        /// Sample response:
        ///
        ///     ["2026-11-03", "2026-11-07"]
        ///
        /// </remarks>
        /// <response code="200">The days with orders, oldest first (can be empty).</response>
        /// <response code="400">month is missing or not in yyyy-MM format.</response>
        /// <response code="401">Not signed in.</response>
        /// <response code="403">Not staff, or a staff member with no working location.</response>
        /// <response code="500">The days couldn't be loaded.</response>
        [HttpGet("days")]
        [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<string>>> GetOrderHistoryDays(
            [FromQuery] string? month,
            [FromQuery(Name = "location_id")] int? locationId = null)
        {
            try
            {
                var scope = await _locationAccess.ResolveLocationAsync(User, locationId);
                if (scope.IsForbidden) return LocationResults.NoLocation();
                locationId = scope.LocationId;

                if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var firstDay))
                {
                    return HistoryBadRequest("month", "Enter a month in yyyy-MM format.");
                }

                var zone = AnalyticsDateRange.RestaurantTimeZone;
                var startUtc = TimeZoneInfo.ConvertTimeToUtc(firstDay.ToDateTime(TimeOnly.MinValue), zone);
                var endUtc = TimeZoneInfo.ConvertTimeToUtc(firstDay.AddMonths(1).ToDateTime(TimeOnly.MinValue), zone);

                var query = _context.SessionOrders
                    .Where(o => o.Created_At >= startUtc && o.Created_At < endUtc);

                if (locationId.HasValue)
                {
                    query = query.Where(o => o.DiningSession.Location_Id == locationId.Value);
                }

                // Only the timestamps come back; the local-day grouping needs the time zone, so it happens here
                var createdAt = await query.Select(o => o.Created_At).ToListAsync();

                var days = createdAt
                    .Select(c => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
                        DateTime.SpecifyKind(c, DateTimeKind.Utc), zone)))
                    .Distinct()
                    .Order()
                    .Select(d => d.ToString("yyyy-MM-dd"))
                    .ToList();

                return Ok(days);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving order history days");
                return StatusCode(500, "Internal Server Error");
            }
        }

        private BadRequestObjectResult HistoryBadRequest(string key, string message)
        {
            ModelState.AddModelError(key, message);
            return BadRequest(new ValidationProblemDetails(ModelState));
        }
    }
}
