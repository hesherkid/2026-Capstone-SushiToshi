using back_end.controllers;
using back_end.DTO.Analytics;
using back_end.Services.Analytics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace back_end.Controllers
{
    [ApiController]
    [Route("api/analytics")]
    [Authorize(Roles = "Admin")]
    [Tags("Analytics")]
    public class TableTurnoverController : ControllerBase
    {
        private readonly ITableTurnoverService _service;
        private readonly ILogger<TableTurnoverController> _logger;

        public TableTurnoverController(ITableTurnoverService service, ILogger<TableTurnoverController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Busiest seating hour (by parties and by guests), session duration by party size,
        /// and the gap between one session ending and the next starting on the same table.
        /// </summary>
        /// <param name="range">today | yesterday | last7 (default) | last30 | custom. last7 and last30 are full days excluding today.</param>
        /// <param name="from">Custom range start, yyyy-MM-dd (inclusive, restaurant time). Required when range=custom.</param>
        /// <param name="to">Custom range end, yyyy-MM-dd (inclusive, restaurant time). Required when range=custom.</param>
        /// <param name="locationId">Limit to one location. Omit for all locations.</param>
        /// <remarks>
        /// Sample requests:
        ///
        ///     GET /api/analytics/table-turnover
        ///     GET /api/analytics/table-turnover?range=yesterday&amp;locationId=1
        ///     GET /api/analytics/table-turnover?range=custom&amp;from=2026-09-01&amp;to=2026-09-30
        ///
        /// Rules:
        /// - Times are stored in UTC; hours and day boundaries use the restaurant time zone.
        /// - Every figure uses sessions seated (Started_At) in the window.
        /// - Party size = sum of Total_Count over the session's non-cancelled bills. Sessions with no
        ///   bills are counted in sessions_without_party_size and the "unknown" bucket.
        /// - Busiest time = sessions seated per hour. A table-group session counts as one seating.
        ///   Ties go to the earlier hour.
        /// - Duration: closed sessions only, bucketed 1-5, 6+, unknown. Over the time limit = longer than 90 minutes.
        /// - Turnover gap = previous session Ended_At to next session Started_At on the same seating unit.
        ///   Single tables and table groups are reported separately; each group is one unit.
        ///   Only same-day gaps of 0-120 minutes are timed. Longer ones are counted in gaps_over_limit,
        ///   negative ones (overlapping sessions) in overlapping_sessions.
        /// </remarks>
        [HttpGet("table-turnover")]
        [ProducesResponseType(typeof(TableTurnoverResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetTableTurnover(
            [FromQuery] string? range,
            [FromQuery] string? from,
            [FromQuery] string? to,
            [FromQuery] int? locationId)
        {
            try
            {
                var result = await _service.GetAsync(new TableTurnoverQuery(range, from, to, locationId));

                return this.ToActionResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving table turnover");
                return StatusCode(500, new { message = "An error occurred while loading table turnover." });
            }
        }
    }
}