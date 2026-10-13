using back_end.controllers;
using back_end.DTO.Analytics;
using back_end.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace back_end.Controllers
{
    [ApiController]
    [Route("api/analytics")]
    [Authorize(Roles = "Admin")]
    [Tags("Analytics")]
    public class OrderTimingController : ControllerBase
    {
        private readonly IOrderTimingService _service;
        private readonly ILogger<OrderTimingController> _logger;

        public OrderTimingController(IOrderTimingService service, ILogger<OrderTimingController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Session duration, peak ordering hour, guest pacing, order status timings, and slowest / fastest items.
        /// </summary>
        /// <param name="range">today | yesterday | last7 (default) | last30 | custom. last7 and last30 are full days excluding today.</param>
        /// <param name="from">Custom range start, yyyy-MM-dd (inclusive, restaurant time). Required when range=custom.</param>
        /// <param name="to">Custom range end, yyyy-MM-dd (inclusive, restaurant time). Required when range=custom.</param>
        /// <param name="locationId">Limit to one location. Omit for all locations.</param>
        /// <param name="categoryId">Limit the slowest / fastest items to one category. Ranks are calculated within the category.</param>
        /// <param name="tagId">Limit the slowest / fastest items to items with this tag. Ranks are calculated within the tag.</param>
        /// <param name="top">How many items to return in the slowest and fastest lists (1-25, default 5).</param>
        /// <remarks>
        /// Sample requests:
        ///
        ///     GET /api/analytics/order-timing
        ///     GET /api/analytics/order-timing?range=yesterday&amp;locationId=1
        ///     GET /api/analytics/order-timing?range=custom&amp;from=2026-09-01&amp;to=2026-09-30&amp;categoryId=3
        ///
        /// Rules:
        /// - Times are stored in UTC; day boundaries and hours use the restaurant time zone.
        /// - Session duration and pacing use tables seated (Started_At) in the window. Order timings and items use
        ///   orders placed (Created_At) in the window.
        /// - Session duration: closed sessions only; open sessions are counted in sessions_open.
        ///   Over the time limit = longer than 90 minutes.
        /// - Pacing: one shared order stream per table, so orders are grouped by session, not by bill.
        /// - pending_to_processing = Created_At to Approved_At; processing_to_completed = Approved_At to Completed_At.
        ///   Orders approved before Approved_At was recorded are counted in orders_missing_approval_time and not timed.
        /// - Item speed = order Approved_At to OrderItems.Completed_At, delivered items only. When staff complete the
        ///   whole order at once, every item gets the same time.
        /// - Items need at least 5 deliveries to be ranked; ranked by median minutes, slowest first, then name.
        ///   Slowest and fastest never overlap.
        /// - categoryId and tagId filter the item lists only, not the session and order figures.
        /// - Cancelled orders are left out. Times where the end is before the start are ignored.
        /// </remarks>
        [HttpGet("order-timing")]
        [ProducesResponseType(typeof(OrderTimingResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetOrderTiming(
            [FromQuery] string? range,
            [FromQuery] string? from,
            [FromQuery] string? to,
            [FromQuery] int? locationId,
            [FromQuery] int? categoryId,
            [FromQuery] int? tagId,
            [FromQuery] int top = 5)
        {
            try
            {
                var result = await _service.GetAsync(
                    new OrderTimingQuery(range, from, to, locationId, categoryId, tagId, top));

                return this.ToActionResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving order timing");
                return StatusCode(500, new { message = "An error occurred while loading order timing." });
            }
        }

    }
}