using back_end.DTO.Analytics;
using back_end.Helpers;
using back_end.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace back_end.controllers
{
    [ApiController]
    [Route("api/analytics")] // literal, not [controller]: the URL stays /api/analytics/browsing-behavior
    [Authorize(Roles = "Admin")]
    [Tags("Analytics")]
    public class BrowsingBehaviorController : ControllerBase
    {
        private readonly IBrowsingBehaviorService _service;
        private readonly ILogger<BrowsingBehaviorController> _logger;

        public BrowsingBehaviorController(IBrowsingBehaviorService service, ILogger<BrowsingBehaviorController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// How diners browse the menu for a reporting window: what they look at, what they order after
        /// looking, and what they look at but don't order.
        /// </summary>
        /// <param name="range">today | yesterday | last7 (default) | last30 | custom. last7 and last30 are full days excluding today.</param>
        /// <param name="from">Custom range start, yyyy-MM-dd (inclusive, restaurant time). Required when range=custom.</param>
        /// <param name="to">Custom range end, yyyy-MM-dd (inclusive, restaurant time). Required when range=custom.</param>
        /// <param name="locationId">Limit to one location. Omit for all locations.</param>
        /// <param name="categoryId">Limit to one category.</param>
        /// <param name="tagId">Limit to items with this tag.</param>
        /// <param name="top">How many items in each top / bottom list (1-25, default 5).</param>
        /// <param name="minViews">Fewest views for an item to count as "high views" (1-1000, default 10).</param>
        /// <remarks>
        /// Sample requests:
        ///
        ///     GET /api/analytics/browsing-behavior
        ///     GET /api/analytics/browsing-behavior?range=yesterday&amp;locationId=1
        ///     GET /api/analytics/browsing-behavior?range=custom&amp;from=2026-10-01&amp;to=2026-10-07&amp;categoryId=3&amp;minViews=20
        ///
        /// Rules:
        /// - A view counts when it lasted at least 5 seconds. Shorter views are reported only as short_views.
        /// - A view belongs to the window by when it started (UTC; day boundaries use the restaurant
        ///   time zone). Location comes from the dining session the view was made in.
        /// - Viewed and ordered = the same dining session has a Delivered order item for that item.
        ///   The order can be before or after the view and can finish after the window ends.
        /// - Conversion rate = ordering sessions / viewing sessions for the item.
        /// - low_conversion_high_views = items with at least minViews views, lowest conversion first.
        /// - Unavailable views = counted views made while the item or its menu assignment was Unavailable.
        /// - Items included: anything viewed in the window, plus anything orderable now.
        /// - never_viewed only lists items orderable now.
        /// </remarks>
        [HttpGet("browsing-behavior")]
        [ProducesResponseType(typeof(BrowsingBehaviorResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetBrowsingBehavior(
            [FromQuery] string? range,
            [FromQuery] string? from,
            [FromQuery] string? to,
            [FromQuery] int? locationId,
            [FromQuery] int? categoryId,
            [FromQuery] int? tagId,
            [FromQuery] int top = 5,
            [FromQuery] int minViews = ViewTrackingRules.DefaultMinViews)
        {
            try
            {
                var result = await _service.GetAsync(
                    new BrowsingBehaviorQuery(range, from, to, locationId, categoryId, tagId, top, minViews));

                return this.ToActionResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving browsing behavior");
                return StatusCode(500, new { message = "An error occurred while loading browsing behavior." });
            }
        }
    }
}