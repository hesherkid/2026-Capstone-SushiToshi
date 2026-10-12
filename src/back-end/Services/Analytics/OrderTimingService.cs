using back_end.domain.DbContexts;
using back_end.domain.enums;
using back_end.DTO.Analytics;
using Microsoft.EntityFrameworkCore;

namespace back_end.Services.Analytics
{
    public sealed record OrderTimingQuery(
        string? Range,
        string? From,
        string? To,
        int? LocationId,
        int? CategoryId,
        int? TagId,
        int Top = 5);

    public interface IOrderTimingService
    {
        Task<AnalyticsResult<OrderTimingResponseDTO>> GetAsync(OrderTimingQuery query);
    }

    public sealed class OrderTimingService : IOrderTimingService
    {
        public const int TimeLimitMinutes = 90;

        public const int MinDeliveries = 5;

        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly TimeProvider _clock;

        public OrderTimingService(ApplicationDbContext context, IConfiguration config, TimeProvider clock)
        {
            _context = context;
            _config = config;
            _clock = clock;
        }

        public async Task<AnalyticsResult<OrderTimingResponseDTO>> GetAsync(OrderTimingQuery query)
        {
            var topError = AnalyticsQueries.ValidateTop<OrderTimingResponseDTO>(query.Top);
            if (topError != null) return topError;

            var windowResult = await AnalyticsQueries.ResolveWindowAsync(
                _context, _config, _clock, query.Range, query.From, query.To, query.LocationId);
            if (!windowResult.IsOk) return windowResult.AsError<OrderTimingResponseDTO>();

            var (period, timeZone, location) = windowResult.Value!;

            var response = new OrderTimingResponseDTO
            {
                Range = AnalyticsQueries.ToRangeDto(period),
                Location = location
            };
            response.Summary.TimeLimitMinutes = TimeLimitMinutes;
            response.Summary.MinDeliveries = MinDeliveries;

            await AddSessionsAndPacingAsync(response, period.StartUtc, period.EndUtc, query.LocationId);
            await AddOrdersAsync(response, period.StartUtc, period.EndUtc, query.LocationId, timeZone);
            await AddItemSpeedAsync(response, period.StartUtc, period.EndUtc, query);

            return AnalyticsResult<OrderTimingResponseDTO>.Ok(response);
        }

        private async Task AddSessionsAndPacingAsync(
            OrderTimingResponseDTO response, DateTime startUtc, DateTime endUtc, int? locationId)
        {
            var sessionsQuery = _context.DiningSessions
                .AsNoTracking()
                .Where(s => s.Started_At >= startUtc && s.Started_At < endUtc);

            var ordersQuery = _context.SessionOrders
                .AsNoTracking()
                .Where(o => o.Status != OrderStatus.Cancelled
                    && o.DiningSession.Started_At >= startUtc
                    && o.DiningSession.Started_At < endUtc);

            if (locationId.HasValue)
            {
                var locId = locationId.Value;
                sessionsQuery = sessionsQuery.Where(s => s.Location_Id == locId);
                ordersQuery = ordersQuery.Where(o => o.DiningSession.Location_Id == locId);
            }

            var sessions = await sessionsQuery
                .Select(s => new { s.Session_Id, s.Started_At, s.Ended_At })
                .ToListAsync();

            var closedSeconds = sessions
                .Where(s => s.Ended_At.HasValue)
                .Select(s => (s.Ended_At!.Value - s.Started_At).TotalSeconds)
                .ToList();

            response.Summary.SessionsStarted = sessions.Count;
            response.Summary.SessionsOpen = sessions.Count(s => !s.Ended_At.HasValue);
            response.Summary.SessionsOverTimeLimit = closedSeconds.Count(seconds => seconds > TimeLimitMinutes * 60);
            response.SessionDuration = AnalyticsQueries.Durations(closedSeconds);

            var startedAt = sessions.ToDictionary(s => s.Session_Id, s => s.Started_At);

            var ordersBySession = (await ordersQuery.Select(o => new { o.session_id, o.Created_At }).ToListAsync())
                .GroupBy(o => o.session_id)
                .ToDictionary(g => g.Key, g => g.Select(o => o.Created_At).OrderBy(t => t).ToList());

            var firstOrderSeconds = new List<double>();
            var gapSeconds = new List<double>();

            foreach (var (sessionId, times) in ordersBySession)
            {
                firstOrderSeconds.Add((times[0] - startedAt[sessionId]).TotalSeconds);

                for (var i = 1; i < times.Count; i++)
                {
                    gapSeconds.Add((times[i] - times[i - 1]).TotalSeconds);
                }
            }

            response.TimeToFirstOrder = AnalyticsQueries.Durations(firstOrderSeconds);
            response.GapBetweenOrders = AnalyticsQueries.Durations(gapSeconds);
            response.AverageOrdersPerSession = ordersBySession.Count == 0
                ? null
                : Math.Round((decimal)ordersBySession.Values.Average(t => t.Count), 1, MidpointRounding.AwayFromZero);
        }

        private async Task AddOrdersAsync(
            OrderTimingResponseDTO response, DateTime startUtc, DateTime endUtc, int? locationId, TimeZoneInfo timeZone)
        {
            var ordersQuery = _context.SessionOrders
                .AsNoTracking()
                .Where(o => o.Status != OrderStatus.Cancelled && o.Created_At >= startUtc && o.Created_At < endUtc);

            if (locationId.HasValue)
            {
                var locId = locationId.Value;
                ordersQuery = ordersQuery.Where(o => o.DiningSession.Location_Id == locId);
            }

            var orders = await ordersQuery
                .Select(o => new { o.Status, o.Created_At, o.Approved_At, o.Completed_At })
                .ToListAsync();

            var ordersPerHour = orders
                .GroupBy(o => AnalyticsQueries.ToLocal(o.Created_At, timeZone).Hour)
                .ToDictionary(g => g.Key, g => g.Count());

            response.OrdersByHour = Enumerable.Range(0, 24)
                .Select(hour => new OrderingHourDTO
                {
                    Hour = hour,
                    Label = $"{hour:00}:00",
                    Orders = ordersPerHour.TryGetValue(hour, out var count) ? count : 0
                })
                .ToList();

            response.PeakHour = response.OrdersByHour
                .Where(h => h.Orders > 0)
                .OrderByDescending(h => h.Orders)
                .ThenBy(h => h.Hour)
                .FirstOrDefault();

            response.PendingToProcessing = AnalyticsQueries.Durations(orders
                .Where(o => o.Approved_At.HasValue)
                .Select(o => (o.Approved_At!.Value - o.Created_At).TotalSeconds));

            response.ProcessingToCompleted = AnalyticsQueries.Durations(orders
                .Where(o => o.Status == OrderStatus.Delivered && o.Approved_At.HasValue && o.Completed_At.HasValue)
                .Select(o => (o.Completed_At!.Value - o.Approved_At!.Value).TotalSeconds));

            response.Summary.OrdersPlaced = orders.Count;
            response.Summary.OrdersMissingApprovalTime = orders.Count(o =>
                (o.Status == OrderStatus.Processing || o.Status == OrderStatus.Delivered) && !o.Approved_At.HasValue);
        }

        private async Task AddItemSpeedAsync(
            OrderTimingResponseDTO response, DateTime startUtc, DateTime endUtc, OrderTimingQuery query)
        {
            var linesQuery = _context.OrderItems
                .AsNoTracking()
                .Where(oi => oi.Order_Item_Status == OrderStatus.Delivered
                    && oi.Completed_At != null
                    && oi.SessionOrder.Approved_At != null
                    && oi.SessionOrder.Created_At >= startUtc
                    && oi.SessionOrder.Created_At < endUtc);

            if (query.LocationId.HasValue)
            {
                var locId = query.LocationId.Value;
                linesQuery = linesQuery.Where(oi => oi.SessionOrder.DiningSession.Location_Id == locId);
            }

            var lines = await linesQuery
                .Select(oi => new
                {
                    oi.Item_Id,
                    ApprovedAt = oi.SessionOrder.Approved_At!.Value,
                    CompletedAt = oi.Completed_At!.Value
                })
                .ToListAsync();

            var secondsByItem = lines
                .GroupBy(l => l.Item_Id)
                .ToDictionary(g => g.Key, g => g.Select(l => (l.CompletedAt - l.ApprovedAt).TotalSeconds).ToList());

            var catalog = await AnalyticsQueries.LoadCatalogAsync(_context, query.LocationId, secondsByItem.Keys);

            var ranked = catalog.Filter(query.CategoryId, query.TagId)
                .Where(i => secondsByItem.ContainsKey(i.ItemId))
                .Select(i =>
                {
                    var times = AnalyticsQueries.Durations(secondsByItem[i.ItemId]);
                    return new ItemTimingDTO
                    {
                        ItemId = i.ItemId,
                        Name = i.Name,
                        CategoryId = i.CategoryId,
                        CategoryName = i.CategoryName,
                        Tags = catalog.TagsFor(i.ItemId),
                        Deliveries = times.Count,
                        AverageMinutes = times.AverageMinutes ?? 0,
                        MedianMinutes = times.MedianMinutes ?? 0
                    };
                })
                .Where(i => i.Deliveries >= MinDeliveries)
                .OrderByDescending(i => i.MedianMinutes)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (var i = 0; i < ranked.Count; i++)
            {
                ranked[i].Rank = i > 0 && ranked[i].MedianMinutes == ranked[i - 1].MedianMinutes
                    ? ranked[i - 1].Rank
                    : i + 1;
            }

            response.SlowestItems = ranked.Take(query.Top).ToList();

            var fastestCount = Math.Min(query.Top, Math.Max(0, ranked.Count - query.Top));
            response.FastestItems = ranked.Skip(ranked.Count - fastestCount).Reverse().ToList();

            response.Summary.ItemsRanked = ranked.Count;
            response.Filters = catalog.BuildFilters();
        }
    }
}