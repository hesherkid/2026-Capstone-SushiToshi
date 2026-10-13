using back_end.domain.DbContexts;
using back_end.domain.enums;
using back_end.DTO.Analytics;
using back_end.Helpers;
using Microsoft.EntityFrameworkCore;

namespace back_end.Services.Analytics
{
    public sealed record BrowsingBehaviorQuery(
        string? Range,
        string? From,
        string? To,
        int? LocationId,
        int? CategoryId,
        int? TagId,
        int Top = 5,
        int MinViews = ViewTrackingRules.DefaultMinViews);

    public interface IBrowsingBehaviorService
    {
        Task<AnalyticsResult<BrowsingBehaviorResponseDTO>> GetAsync(BrowsingBehaviorQuery query);
    }

    public sealed class BrowsingBehaviorService : IBrowsingBehaviorService
    {
        private readonly ApplicationDbContext _context;
        private readonly TimeProvider _clock;

        public BrowsingBehaviorService(ApplicationDbContext context, TimeProvider clock)
        {
            _context = context;
            _clock = clock;
        }

        public async Task<AnalyticsResult<BrowsingBehaviorResponseDTO>> GetAsync(BrowsingBehaviorQuery query)
        {
            var topError = AnalyticsQueries.ValidateTop<BrowsingBehaviorResponseDTO>(query.Top);
            if (topError != null) return topError;

            if (query.MinViews < 1 || query.MinViews > ViewTrackingRules.MaxMinViews)
            {
                return AnalyticsResult<BrowsingBehaviorResponseDTO>.Invalid(
                    "minViews", $"minViews must be between 1 and {ViewTrackingRules.MaxMinViews}");
            }

            var windowResult = await AnalyticsQueries.ResolveWindowAsync(
                _context, _clock, query.Range, query.From, query.To, query.LocationId);
            if (!windowResult.IsOk) return windowResult.AsError<BrowsingBehaviorResponseDTO>();

            var (period, _, location) = windowResult.Value!;
            var top = query.Top;
            var minViews = query.MinViews;

            var startUtc = period.StartUtc;
            var endUtc = period.EndUtc;
            const int minSeconds = ViewTrackingRules.MinQualifyingSeconds;

            var windowViews = _context.MenuItemViews
                .AsNoTracking()
                .Where(v => v.Viewed_At >= startUtc && v.Viewed_At < endUtc);

            if (query.LocationId.HasValue)
            {
                var locId = query.LocationId.Value;
                windowViews = windowViews.Where(v => v.Location_Id == locId);
            }

            var countedViews = windowViews.Where(v => v.View_Seconds >= minSeconds);

            var shortViewsByItem = await windowViews
                .Where(v => v.View_Seconds < minSeconds)
                .GroupBy(v => v.Item_Id)
                .Select(g => new { ItemId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ItemId, x => x.Count);

            var viewPairs = await countedViews
                .GroupBy(v => new { v.Item_Id, v.Session_Id })
                .Select(g => new
                {
                    ItemId = g.Key.Item_Id,
                    SessionId = g.Key.Session_Id,
                    Views = g.Count(),
                    Seconds = g.Sum(v => v.View_Seconds),
                    UnavailableViews = g.Sum(v => v.Was_Available ? 0 : 1)
                })
                .ToListAsync();

            var orderedPairs = await _context.OrderItems
                .AsNoTracking()
                .Where(oi => oi.Order_Item_Status == OrderStatus.Delivered
                    && countedViews.Any(v => v.Session_Id == oi.SessionOrder.session_id && v.Item_Id == oi.Item_Id))
                .Select(oi => new { oi.Item_Id, oi.Quantity, SessionId = oi.SessionOrder.session_id })
                .GroupBy(x => new { x.Item_Id, x.SessionId })
                .Select(g => new { ItemId = g.Key.Item_Id, g.Key.SessionId, Units = g.Sum(x => x.Quantity) })
                .ToListAsync();

            var orderedUnitsByPair = orderedPairs.ToDictionary(p => (p.ItemId, p.SessionId), p => p.Units);
            bool PairOrdered(int itemId, int sessionId) => orderedUnitsByPair.ContainsKey((itemId, sessionId));

            var viewedItemIds = viewPairs.Select(p => p.ItemId).Concat(shortViewsByItem.Keys);
            var catalog = await AnalyticsQueries.LoadCatalogAsync(_context, query.LocationId, viewedItemIds);

            var pairsByItem = viewPairs.ToLookup(p => p.ItemId);

            var rows = catalog.Filter(query.CategoryId, query.TagId)
                .Select(i =>
                {
                    var pairs = pairsByItem[i.ItemId].ToList();
                    var ordered = pairs.Where(p => PairOrdered(p.ItemId, p.SessionId)).ToList();
                    var notOrdered = pairs.Where(p => !PairOrdered(p.ItemId, p.SessionId)).ToList();
                    var views = pairs.Sum(p => p.Views);
                    var seconds = pairs.Sum(p => p.Seconds);

                    return new BrowsingItemDTO
                    {
                        ItemId = i.ItemId,
                        Name = i.Name,
                        CategoryId = i.CategoryId,
                        CategoryName = i.CategoryName,
                        Status = i.Status.ToString(),
                        CurrentlyAvailable = i.CurrentlyAvailable,
                        Tags = catalog.TagsFor(i.ItemId),
                        Views = views,
                        ShortViews = shortViewsByItem.TryGetValue(i.ItemId, out var shortViews) ? shortViews : 0,
                        TotalViewSeconds = seconds,
                        AverageViewSeconds = AnalyticsQueries.SecondsPerView(seconds, views),
                        ViewingSessions = pairs.Count,
                        OrderingSessions = ordered.Count,
                        ConversionRate = AnalyticsQueries.Percent(ordered.Count, pairs.Count),
                        UnitsOrderedAfterView = ordered.Sum(p => orderedUnitsByPair[(p.ItemId, p.SessionId)]),
                        AverageViewSecondsOrdered = AnalyticsQueries.SecondsPerView(ordered.Sum(p => p.Seconds), ordered.Sum(p => p.Views)),
                        AverageViewSecondsNotOrdered = AnalyticsQueries.SecondsPerView(notOrdered.Sum(p => p.Seconds), notOrdered.Sum(p => p.Views)),
                        UnavailableViews = pairs.Sum(p => p.UnavailableViews)
                    };
                })
                .ToList();

            var viewed = rows
                .Where(r => r.Views > 0)
                .OrderByDescending(r => r.Views)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var i = 0; i < viewed.Count; i++)
            {
                viewed[i].ViewRank = i > 0 && viewed[i].Views == viewed[i - 1].Views
                    ? viewed[i - 1].ViewRank
                    : i + 1;
            }

            var unviewedRows = rows
                .Where(r => r.Views == 0)
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var leastCount = Math.Min(top, Math.Max(0, viewed.Count - top));
            var viewedNotOrdered = viewed.Where(r => r.OrderingSessions == 0).ToList();
            var neverViewed = unviewedRows.Where(r => r.CurrentlyAvailable).ToList();

            var includedIds = rows.Select(r => r.ItemId).ToHashSet();
            var includedPairs = viewPairs.Where(p => includedIds.Contains(p.ItemId)).ToList();
            var includedOrdered = includedPairs.Where(p => PairOrdered(p.ItemId, p.SessionId)).ToList();
            var includedNotOrdered = includedPairs.Where(p => !PairOrdered(p.ItemId, p.SessionId)).ToList();
            var totalViews = includedPairs.Sum(p => p.Views);
            var totalSeconds = includedPairs.Sum(p => p.Seconds);

            return AnalyticsResult<BrowsingBehaviorResponseDTO>.Ok(new BrowsingBehaviorResponseDTO
            {
                Range = AnalyticsQueries.ToRangeDto(period),
                Location = location,
                Summary = new BrowsingSummaryDTO
                {
                    TotalViews = totalViews,
                    ShortViewsExcluded = rows.Sum(r => r.ShortViews),
                    TotalViewSeconds = totalSeconds,
                    AverageViewSeconds = AnalyticsQueries.SecondsPerView(totalSeconds, totalViews),
                    AverageViewSecondsOrdered = AnalyticsQueries.SecondsPerView(includedOrdered.Sum(p => p.Seconds), includedOrdered.Sum(p => p.Views)),
                    AverageViewSecondsNotOrdered = AnalyticsQueries.SecondsPerView(includedNotOrdered.Sum(p => p.Seconds), includedNotOrdered.Sum(p => p.Views)),
                    ViewingSessions = includedPairs.Select(p => p.SessionId).Distinct().Count(),
                    SessionItemViews = includedPairs.Count,
                    SessionItemOrders = includedOrdered.Count,
                    ConversionRate = AnalyticsQueries.Percent(includedOrdered.Count, includedPairs.Count),
                    ItemsConsidered = rows.Count,
                    ItemsViewed = viewed.Count,
                    ItemsNeverViewed = neverViewed.Count,
                    UnavailableViews = rows.Sum(r => r.UnavailableViews),
                    MinViewSeconds = minSeconds,
                    MinViews = minViews
                },
                MostViewed = viewed.Take(top).ToList(),
                LeastViewed = viewed.Skip(viewed.Count - leastCount).Reverse().ToList(),
                NeverViewed = neverViewed,
                ViewedNotOrdered = viewedNotOrdered,
                TopViewedNotOrdered = viewedNotOrdered.Take(top).ToList(),
                TopViewedAndOrdered = viewed
                    .Where(r => r.OrderingSessions > 0)
                    .OrderByDescending(r => r.OrderingSessions)
                    .ThenByDescending(r => r.ConversionRate)
                    .ThenByDescending(r => r.Views)
                    .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(top)
                    .ToList(),
                LowConversionHighViews = viewed
                    .Where(r => r.Views >= minViews)
                    .OrderBy(r => r.ConversionRate ?? 0)
                    .ThenByDescending(r => r.Views)
                    .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(top)
                    .ToList(),
                UnavailableViews = rows
                    .Where(r => r.UnavailableViews > 0)
                    .OrderByDescending(r => r.UnavailableViews)
                    .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Items = viewed.Concat(unviewedRows).ToList(),
                Filters = catalog.BuildFilters()
            });
        }
    }
}