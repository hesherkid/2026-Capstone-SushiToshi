using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using Microsoft.AspNetCore.Mvc;
using back_end.DTO.Analytics;
using back_end.Helpers;


namespace back_end.controllers
{
    /// TODO:Analytics endpoints not yet moved to their own controller and service table turnover, order timing. 
    [ApiController]
    [Route("api/analytics")] // literal so renaming or splitting this class can't change the URLs
    [Authorize(Roles = "Admin")]
    public class AnalyticsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AnalyticsController> _logger;

        public AnalyticsController(ApplicationDbContext context, ILogger<AnalyticsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves table turnover metrics showing average dining duration grouped by day and month.
        /// </summary>
        /// <param name="partySize">The party size to filter metrics by (default: 2)</param>
        /// <returns>
        /// An <see cref="IActionResult"/> containing a <see cref="TableTurnOverResponseDTO"/> object with daily and monthly metrics.
        /// Returns HTTP 200 (OK) with turnover metrics on success.
        /// Returns HTTP 404 (Not Found) if no data is available for the specified party size.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
        /// </returns>
        /// <response code="200">Returns table turnover metrics grouped by day and month</response>
        /// <response code="404">If no turnover data is found for the specified party size</response>
        /// <response code="500">If an internal error occurs while retrieving metrics</response>
        /// <remarks>
        /// Sample requests:
        ///
        ///     GET /api/analytics/table-turnover
        ///     (Returns metrics for party size of 2)
        ///     
        ///     GET /api/analytics/table-turnover?partySize=4
        ///     (Returns metrics for party size of 4)
        ///
        /// Calculates average dining duration (from bill creation to closure) in minutes.
        /// Party size is calculated as the sum of seniors, adults, and children on each bill.
        /// Only includes closed bills (bills with a Closed_At timestamp).
        /// 
        /// Returns two datasets:
        /// - Daily: Average duration per day (format: yyyy-MM-dd)
        /// - Monthly: Average duration per month (format: yyyy-MM)
        /// </remarks>
        [HttpGet("table-turnover")]
        [ProducesResponseType(typeof(TableTurnOverResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetTableTurnoverMetrics([FromQuery] int partySize = 2)
        {
            try
            {
                // Filter out bills with null Closed_At because we need to group by Closed_At date/time
                var filteredBills = _context.Bills
                    .Where(b => b.Closed_At.HasValue);
                // .Where(b => (b.Senior_Count + b.Adult_Count + b.Child_Count) == partySize);

                // Daily turnover query
                var dailyTurnover = await filteredBills
                    .GroupBy(b => new
                    {
                        Day = b.Closed_At!.Value.Date,
                        PartySize = b.Senior_Count + b.Adult_Count + b.Child_Count
                    })
                    .Select(g => new TurnoverMetricDTO
                    {
                        Period = g.Key.Day.ToString("yyyy-MM-dd"),
                        AverageDuration = (int)Math.Round(
                            g.Average(b => EF.Functions.DateDiffMinute(b.Created_At, b.Closed_At!.Value))),
                        PartySize = g.Key.PartySize
                    })
                    .ToListAsync();

                // Monthly turnover query
                var monthlyTurnover = await filteredBills
                    .GroupBy(b => new
                    {
                        Month = new DateTime(b.Closed_At!.Value.Year, b.Closed_At!.Value.Month, 1),
                        PartySize = b.Senior_Count + b.Adult_Count + b.Child_Count
                    })
                    .Select(g => new TurnoverMetricDTO
                    {
                        Period = g.Key.Month.ToString("yyyy-MM"),
                        AverageDuration = (int)Math.Round(
                            g.Average(b => EF.Functions.DateDiffMinute(b.Created_At, b.Closed_At!.Value))),
                        PartySize = g.Key.PartySize
                    })
                    .ToListAsync();


                if (!dailyTurnover.Any() && !monthlyTurnover.Any())
                {
                    _logger.LogWarning("No table turnover data found");
                    return NotFound(new { message = "No table turnover data found" });
                }

                _logger.LogInformation($"Retrieved turnover data: {dailyTurnover.Count} daily records, {monthlyTurnover.Count} monthly records");

                var response = new TableTurnOverResponseDTO
                {
                    Daily = dailyTurnover.Select(d => new TableTurnoverDailyDTO
                    {
                        Day = d.Period ?? string.Empty,
                        AverageDuration = d.AverageDuration,
                        Party_size = d.PartySize
                    }).ToList(),
                    Monthly = monthlyTurnover.Select(m => new TableTurnOverMonthlyDTO
                    {
                        Month = m.Period ?? string.Empty,
                        AverageDuration = m.AverageDuration,
                        Party_size = m.PartySize
                    }).ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving table turnover metrics: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
            }
        }



        [HttpGet("order-timing/{location_id}")]
        [ProducesResponseType(typeof(TableTurnOverResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetOrderTimingMetrics(
            string location_id
        )
        {
            try
            {
                // Use ClaimsHelpers for user identification
                string userIdString = ClaimsHelpers.GetUserId(User);

                // Get user's location from claims (if available)
                // string? locationIdStr = User.FindFirst("location_id")?.Value;
                string locationIdStr = location_id;
                int? locationId = null;
                if (int.TryParse(locationIdStr, out var locId))
                    locationId = locId;

                if (locationId == null)
                {
                    return BadRequest("Cannot determine user location from claims.");
                }

                var orderTiming = await _context.DiningSessions
                    .Where(ds => ds.Location_Id == locationId)
                    .Select(ds => new
                    {
                        sessionId = ds.Session_Id,
                        TimeToFirstOrderSeconds = EF.Functions.DateDiffSecond(ds.Started_At, ds.First_Order_At)
                    }).Take(25)
                    .ToListAsync();

                var dailyAverageTiming = await _context.DiningSessions
                    .GroupBy(ds => ds.Started_At.Date)
                    .Select(g => new
                    {
                        Day = g.Key,
                        AverageTimeToFirstOrderSeconds = g.Average(ds => EF.Functions.DateDiffSecond(ds.Started_At, ds.First_Order_At))
                    })
                    .ToListAsync();

                return Ok(new { orderTiming, dailyAverageTiming });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving order timing metrics: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
            }
        }

        [HttpGet("max-party-size")]
        public async Task<IActionResult> GetMaxPartySize()
        {
            var maxPartySize = _context.Bills.Max(b => b.Child_Count + b.Adult_Count + b.Senior_Count);
            return Ok(maxPartySize);
        }
    }
}