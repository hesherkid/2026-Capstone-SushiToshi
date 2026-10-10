using back_end.DTO.Analytics;
using back_end.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace back_end.controllers
{
    [ApiController]
    [Route("api/analytics")] // literal, not [controller]: the URL stays /api/analytics/item-performance
    [Authorize(Roles = "Admin")]
    public class ItemPerformanceController : ControllerBase
    {
        private readonly IItemPerformanceService _service;
        private readonly ILogger<ItemPerformanceController> _logger;

        public ItemPerformanceController(IItemPerformanceService service, ILogger<ItemPerformanceController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Units sold per menu item for a reporting window, with best / worst / zero-sales breakdown.
        /// </summary>
        /// <param name="range">today | yesterday | last7 (default) | last30 | custom. last7 and last30 are full days excluding today.</param>
        /// <param name="from">Custom range start, yyyy-MM-dd (inclusive, restaurant time). Required when range=custom.</param>
        /// <param name="to">Custom range end, yyyy-MM-dd (inclusive, restaurant time). Required when range=custom.</param>
        /// <param name="locationId">Limit to one location. Omit for all locations.</param>
        /// <param name="categoryId">Limit to one category. Ranks are calculated within the category.</param>
        /// <param name="tagId">Limit to items with this tag. Ranks are calculated within the tag.</param>
        /// <param name="top">How many items to return in the best and worst lists (1-25, default 5).</param>
        /// <remarks>
        /// Sample requests:
        ///
        ///     GET /api/analytics/item-performance
        ///     GET /api/analytics/item-performance?range=yesterday&amp;locationId=1
        ///     GET /api/analytics/item-performance?range=custom&amp;from=2026-09-01&amp;to=2026-09-30&amp;categoryId=3
        ///
        /// Rules:
        /// - Only order items with status Delivered count as sold (Cancelled items also get Completed_At, so status is checked).
        /// - The sale date is OrderItems.Completed_At, stored in UTC; day boundaries use the restaurant time zone.
        /// - Items included: anything with delivered sales in the window (even if unavailable or off the menu now),
        ///   plus anything currently orderable (not Unavailable, on an active menu; at the location when given).
        ///   Items that are unavailable now and had no sales are excluded, because availability history isn't stored.
        /// - Add-on sales use MenuItemAssignment.Is_Add_On for the (menu, item) the sale was ordered from.
        ///   add_on_days = number of local days in the window with at least one add-on sale.
        /// - Best and worst never overlap; zero-sales items are returned separately.
        /// </remarks>
        /// <response code="200">Item performance for the requested window (lists may be empty)</response>
        /// <response code="400">Invalid range, dates or top value</response>
        /// <response code="404">The locationId does not exist</response>
        /// <response code="500">Unexpected server error</response>
        [HttpGet("item-performance")]
        [ProducesResponseType(typeof(ItemPerformanceResponseDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetItemPerformance(
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
                    new ItemPerformanceQuery(range, from, to, locationId, categoryId, tagId, top));

                return this.ToActionResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving item performance");
                return StatusCode(500, new { message = "An error occurred while loading item performance." });
            }
        }
    }
}