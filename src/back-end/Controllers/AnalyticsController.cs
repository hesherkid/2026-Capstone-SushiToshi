using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using Microsoft.AspNetCore.Mvc;
using back_end.DTO.Analytics;
using Microsoft.AspNetCore.Http.Features;
using back_end.Helpers;
using back_end.domain.enums;


namespace back_end.controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly ILogger<AnalyticsController> _logger;

        public AnalyticsController(ApplicationDbContext context, IConfiguration config, ILogger<AnalyticsController> logger)
        {
            _context = context;
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Units sold per menu item for a repoting window, with best / worst / zero-sales breakdown.
        /// </summary>
        /// <param name=""range"> today | yesterday | last7 (default) | last 30 | custom. last7 & last30 are full days excluding today.
        /// <param name="from">Custom range start, yyyy-MM-dd (inclusive, Mountain Time). Required when range=custom.</param>
        /// <param name="to">Custom range end, yyyy-MM-dd (inclusive, Mountain Time). Required when range=custom.</param>
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
        public async Task<IActionResult> GetItemPerformance([FromQuery] string? range,
            [FromQuery] string? from,
            [FromQuery] string? to,
            [FromQuery] int? locationId,
            [FromQuery] int? categoryId,
            [FromQuery] int? tagId,
            [FromQuery] int top = 5)
        {
            if (top < 1 || top > 25)
            {
                ModelState.AddModelError(nameof(top), "top must be between 1 and 25");
                return ValidationProblem(ModelState);
            }

            try
            {
                var timeZone = GetAnalyticsTimeZone();

                if (!AnalyticsDateRange.TryResolve(range, from, to, DateTime.UtcNow, timeZone, out var period, out var errorKey, out var errorMessage) || period == null)
                {
                    ModelState.AddModelError(errorKey, errorMessage);
                    return ValidationProblem(ModelState);
                }

                ItemPerformanceLocationDTO? location = null;
                if (locationId.HasValue)
                {
                    location = await _context.Locations
                        .AsNoTracking()
                        .Where(l => l.Location_Id == locationId.Value)
                        .Select(l => new ItemPerformanceLocationDTO { Id = l.Location_Id, Name = l.Name })
                        .FirstOrDefaultAsync();

                    if (location == null)
                    {
                        return NotFound(new { message = $"Location {locationId.Value} was not found." });
                    }
                }

                // 1) Delivered sales in the window. Half-open range: >= start AND < end.
                var startUtc = period.StartUtc;
                var endUtc = period.EndUtc;

                var salesQuery = _context.OrderItems
                    .AsNoTracking()
                    .Where(oi => oi.Order_Item_Status == OrderStatus.Delivered
                        && oi.Completed_At >= startUtc
                        && oi.Completed_At < endUtc
                    );

                if (locationId.HasValue)
                {
                    var locId = locationId.Value;
                    salesQuery = salesQuery.Where(oi => oi.SessionOrder.DiningSession.Location_Id == locId);
                }

                // Mark each sale as an add-on using the assignment for the menu it was ordered from.
                // The assignment key is (Menu_Id, Item_Id), so this left join never duplicates rows.
                var salesRows =
                    from oi in salesQuery
                    join a in _context.MenuItemAssignments
                        on new { MenuId = oi.Menu_Id, ItemId = oi.Item_Id }
                        equals new { MenuId = a.Menu_Id, ItemId = a.Item_Id } into assignments
                    from a in assignments.DefaultIfEmpty()
                    select new
                    {
                        oi.Item_Id,
                        oi.Quantity,
                        oi.Completed_At,
                        IsAddOn = a != null && a.Is_Add_On
                    };

                var salesByItem = await salesRows
                    .GroupBy(s => s.Item_Id)
                    .Select(g => new
                    {
                        ItemId = g.Key,
                        Units = g.Sum(s => s.Quantity),
                        AddOnUnits = g.Sum(s => s.IsAddOn ? s.Quantity : 0)
                    })
                    .ToDictionaryAsync(x => x.ItemId);

                // How many different local days each item was sold as an add-on (is it consistently and add on item).
                var addOnSaleTimes = await salesRows
                    .Where(s => s.IsAddOn)
                    .Select(s => new { s.Item_Id, s.Completed_At })
                    .ToListAsync();

                var addOnDaysByItem = addOnSaleTimes
                    .GroupBy(s => s.Item_Id)
                    .ToDictionary(
                        g => g.Key,
                        g => g
                            .Select(s => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
                                DateTime.SpecifyKind(s.Completed_At!.Value, DateTimeKind.Utc), timeZone)))
                            .Distinct()
                            .Count());

                var soldItemIds = salesByItem.Keys.ToList();

                // 2) Items to report:
                //    - anything that sold in the window, even if it's unavailable or off the menu now
                //      (it was clearly orderable at the time), and
                //    - anything orderable right now, so items with no sales still appear.
                //    Items that are unavailable now AND had no sales are left out: without a status
                //    history we can't tell whether they were orderable during the window.
                var candidateItems = await _context.MenuItems
                    .AsNoTracking()
                    .Select(mi => new
                    {
                        mi.item_id,
                        mi.Name,
                        mi.Category_id,
                        CategoryName = mi.Category.Category_name,
                        mi.Status,
                        CurrentlyAvailable =
                            mi.Status != MenuItemStatus.Unavailable &&
                            mi.MenuAssignments.Any(a =>
                                a.Status != MenuItemStatus.Unavailable &&
                                a.Menu.Is_active &&
                                (locationId == null || a.Menu.MenuLocations.Any(ml => ml.Location_Id == locationId)))
                    })
                    .Where(x => x.CurrentlyAvailable || soldItemIds.Contains(x.item_id))
                    .ToListAsync();

                // Tags in a separate query; simpler SQL than a nested collection.
                var candidateIds = candidateItems.Select(i => i.item_id).ToList();
                var tagsByItem = (await _context.MenuItemTags
                        .AsNoTracking()
                        .Where(t => candidateIds.Contains(t.Menu_item_id))
                        .Select(t => new
                        {
                            t.Menu_item_id,
                            Tag = new ItemPerformanceTagDTO
                            {
                                TagId = t.Tag_id,
                                Name = t.Tag.tag_name,
                                Color = t.Tag.tag_color
                            }
                        })
                        .ToListAsync())
                    .GroupBy(t => t.Menu_item_id)
                    .ToDictionary(g => g.Key, g => g.Select(t => t.Tag).OrderBy(t => t.Name).ToList());

                List<ItemPerformanceTagDTO> TagsFor(int itemId) =>
                    tagsByItem.TryGetValue(itemId, out var tags) ? tags : new List<ItemPerformanceTagDTO>();

                // Dropdown options come from the full candidate list, before category/tag filtering,
                // so choosing a category doesn't remove the other categories from the dropdown.
                var filters = new ItemPerformanceFiltersDTO
                {
                    Categories = candidateItems
                        .GroupBy(i => new { i.Category_id, i.CategoryName })
                        .Select(g => new ItemPerformanceFilterOptionDTO { Id = g.Key.Category_id, Name = g.Key.CategoryName })
                        .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    Tags = tagsByItem.Values
                        .SelectMany(t => t)
                        .GroupBy(t => t.TagId)
                        .Select(g => g.First())
                        .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };

                // 3) Combine and rank in memory (menu-sized lists).
                var rows = candidateItems
                    .Where(i => categoryId == null || i.Category_id == categoryId)
                    .Where(i => tagId == null || TagsFor(i.item_id).Any(t => t.TagId == tagId))
                    .Select(i =>
                    {
                        salesByItem.TryGetValue(i.item_id, out var sales);
                        var units = sales?.Units ?? 0;
                        var addOnUnits = sales?.AddOnUnits ?? 0;

                        return new ItemPerformanceDTO
                        {
                            ItemId = i.item_id,
                            Name = i.Name,
                            CategoryId = i.Category_id,
                            CategoryName = i.CategoryName,
                            Status = i.Status.ToString(),
                            CurrentlyAvailable = i.CurrentlyAvailable,
                            Tags = TagsFor(i.item_id),
                            UnitsSold = units,
                            AddOnUnits = addOnUnits,
                            AddOnShare = units == 0
                                ? 0
                                : Math.Round(addOnUnits * 100m / units, 1, MidpointRounding.AwayFromZero),
                            AddOnDays = addOnDaysByItem.TryGetValue(i.item_id, out var days) ? days : 0
                        };
                    })
                    .ToList();

                var totalUnits = rows.Sum(r => r.UnitsSold);

                var sold = rows
                    .Where(r => r.UnitsSold > 0)
                    .OrderByDescending(r => r.UnitsSold)
                    .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Competition ranking: ties share a rank, the next rank skips (1, 2, 2, 4).
                for (var i = 0; i < sold.Count; i++)
                {
                    sold[i].Rank = i > 0 && sold[i].UnitsSold == sold[i - 1].UnitsSold
                        ? sold[i - 1].Rank
                        : i + 1;
                }

                foreach (var row in rows)
                {
                    row.ShareOfUnits = totalUnits == 0
                        ? 0
                        : Math.Round(row.UnitsSold * 100m / totalUnits, 1, MidpointRounding.AwayFromZero);
                }

                var zeroSales = rows
                    .Where(r => r.UnitsSold == 0)
                    .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var best = sold.Take(top).ToList();

                // Worst comes from whatever is left after Best, so the two lists never repeat an item.
                var worstCount = Math.Min(top, Math.Max(0, sold.Count - top));
                var worst = sold.Skip(sold.Count - worstCount).Reverse().ToList();

                // Items bought as add-ons, most consistent first: most days with add-on sales, then most add-on units.
                var addOns = rows
                    .Where(r => r.AddOnUnits > 0)
                    .OrderByDescending(r => r.AddOnDays)
                    .ThenByDescending(r => r.AddOnUnits)
                    .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var response = new ItemPerformanceResponseDTO
                {
                    Range = new ItemPerformanceRangeDTO
                    {
                        Preset = period.Preset.ToString().ToLowerInvariant(),
                        From = period.FromLocal.ToString("yyyy-MM-dd"),
                        To = period.ToLocal.ToString("yyyy-MM-dd"),
                        Days = period.ToLocal.DayNumber - period.FromLocal.DayNumber + 1,
                        TimeZone = period.TimeZoneId
                    },
                    Location = location,
                    Summary = new ItemPerformanceSummaryDTO
                    {
                        TotalUnitsSold = totalUnits,
                        TotalAddOnUnits = rows.Sum(r => r.AddOnUnits),
                        ItemsConsidered = rows.Count,
                        ItemsWithSales = sold.Count,
                        ItemsWithZeroSales = zeroSales.Count
                    },
                    Best = best,
                    Worst = worst,
                    ZeroSales = zeroSales,
                    AddOns = addOns,
                    Items = sold.Concat(zeroSales).ToList(),
                    Filters = filters
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving item performance");
                return StatusCode(500, new { message = "An error occurred while loading item performance." });
            }
        }

        /// <summary>
        /// Restaurant time zone for day boundaries. Configure "Analytics:TimeZoneId" in appsettings.
        /// </summary>
        private TimeZoneInfo GetAnalyticsTimeZone()
        {
            var id = _config["Analytics:TimeZoneId"];
            return TimeZoneInfo.FindSystemTimeZoneById(
                string.IsNullOrWhiteSpace(id) ? AnalyticsDateRange.DefaultTimeZoneId : id);
        }


        /// <summary>
        /// Retrieves browsing behavior metrics for menu items showing total views and view duration.
        /// </summary>
        /// <returns>
        /// An <see cref="IActionResult"/> containing a collection of browsing behavior data objects.
        /// Returns HTTP 200 (OK) with browsing metrics on success.
        /// Returns HTTP 404 (Not Found) if no browsing data is available.
        /// Returns HTTP 500 (Internal Server Error) if an exception occurs during retrieval.
        /// </returns>
        /// <response code="200">Returns browsing behavior metrics for all menu items</response>
        /// <response code="404">If no browsing behavior data is found</response>
        /// <response code="500">If an internal error occurs while retrieving metrics</response>
        /// <remarks>
        /// Sample request:
        ///
        ///     GET /api/analytics/browsing-behavior
        ///
        /// Returns aggregated metrics across all menu assignments showing:
        /// - Item ID and name
        /// - Total view duration in seconds
        /// - Total number of views across all menus
        /// 
        /// Data is aggregated from all MenuItemAssignments, combining statistics
        /// for the same item across different menus.
        /// </remarks>
        [HttpGet("browsing-behavior")]
        [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetBrowsingBehavior()
        {
            try
            {
                var browsingData = await _context.MenuItemAssignments
                    .Join(
                        _context.MenuItems,
                        assignment => assignment.Item_Id,
                        menuItem => menuItem.item_id,
                        (assignment, menuItem) => new
                        {
                            assignment.Item_Id,
                            menuItem.Name,
                            assignment.Total_View_Seconds,
                            assignment.Total_Views,
                            assignment.Menu_Id
                        })
                    .GroupBy(x => new { x.Item_Id, x.Name })
                    .Select(g => new
                    {
                        item_id = g.Key.Item_Id,
                        name = g.Key.Name,
                        total_view_seconds = g.Sum(x => x.Total_View_Seconds),
                        total_views = g.Sum(x => x.Total_Views)
                    })
                    .ToListAsync();

                if (browsingData == null || browsingData.Count == 0)
                {
                    return NotFound(new { message = "No browsing behavior data found." });
                }

                return Ok(browsingData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while processing your request.", error = ex.Message });
            }
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
