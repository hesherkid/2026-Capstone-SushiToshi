using back_end.domain.DbContexts;
using back_end.domain.enums;
using back_end.DTO.Analytics;
using Microsoft.EntityFrameworkCore;

namespace back_end.Services.Analytics
{
    public sealed record ItemPerformanceQuery(
        string? Range,
        string? From,
        string? To,
        int? LocationId,
        int? CategoryId,
        int? TagId,
        int Top = 5);

    public interface IItemPerformanceService
    {
        /// <summary>Units sold per menu item for a reporting window, with best / worst / zero-sales breakdown.</summary>
        Task<AnalyticsResult<ItemPerformanceResponseDTO>> GetAsync(ItemPerformanceQuery query);
    }

    public sealed class ItemPerformanceService : IItemPerformanceService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly TimeProvider _clock;

        public ItemPerformanceService(ApplicationDbContext context, IConfiguration config, TimeProvider clock)
        {
            _context = context;
            _config = config;
            _clock = clock;
        }

        public async Task<AnalyticsResult<ItemPerformanceResponseDTO>> GetAsync(ItemPerformanceQuery query)
        {
            var topError = AnalyticsQueries.ValidateTop<ItemPerformanceResponseDTO>(query.Top);
            if (topError != null) return topError;

            var windowResult = await AnalyticsQueries.ResolveWindowAsync(
                _context, _config, _clock, query.Range, query.From, query.To, query.LocationId);
            if (!windowResult.IsOk) return windowResult.AsError<ItemPerformanceResponseDTO>();

            var (period, timeZone, location) = windowResult.Value!;

            // Delivered sales in the window. Half-open range: >= start AND < end.
            var startUtc = period.StartUtc;
            var endUtc = period.EndUtc;

            var salesQuery = _context.OrderItems
                .AsNoTracking()
                .Where(oi => oi.Order_Item_Status == OrderStatus.Delivered
                    && oi.Completed_At >= startUtc
                    && oi.Completed_At < endUtc);

            if (query.LocationId.HasValue)
            {
                var locId = query.LocationId.Value;
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

            // How many different local days each item was sold as an add-on (is it consistently an add-on item).
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

            // Items to report: anything sold in the window, plus anything orderable now.
            var catalog = await AnalyticsQueries.LoadCatalogAsync(_context, query.LocationId, salesByItem.Keys);

            // Combine and rank in memory (menu-sized lists).
            var rows = catalog.Filter(query.CategoryId, query.TagId)
                .Select(i =>
                {
                    salesByItem.TryGetValue(i.ItemId, out var sales);
                    var units = sales?.Units ?? 0;
                    var addOnUnits = sales?.AddOnUnits ?? 0;

                    return new ItemPerformanceDTO
                    {
                        ItemId = i.ItemId,
                        Name = i.Name,
                        CategoryId = i.CategoryId,
                        CategoryName = i.CategoryName,
                        Status = i.Status.ToString(),
                        CurrentlyAvailable = i.CurrentlyAvailable,
                        Tags = catalog.TagsFor(i.ItemId),
                        UnitsSold = units,
                        AddOnUnits = addOnUnits,
                        AddOnShare = AnalyticsQueries.Percent(addOnUnits, units) ?? 0,
                        AddOnDays = addOnDaysByItem.TryGetValue(i.ItemId, out var days) ? days : 0
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
                row.ShareOfUnits = AnalyticsQueries.Percent(row.UnitsSold, totalUnits) ?? 0;
            }

            var zeroSales = rows
                .Where(r => r.UnitsSold == 0)
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var best = sold.Take(query.Top).ToList();

            // Worst comes from whatever is left after Best, so the two lists never repeat an item.
            var worstCount = Math.Min(query.Top, Math.Max(0, sold.Count - query.Top));
            var worst = sold.Skip(sold.Count - worstCount).Reverse().ToList();

            // Items bought as add-ons, most consistent first: most days with add-on sales, then most add-on units.
            var addOns = rows
                .Where(r => r.AddOnUnits > 0)
                .OrderByDescending(r => r.AddOnDays)
                .ThenByDescending(r => r.AddOnUnits)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return AnalyticsResult<ItemPerformanceResponseDTO>.Ok(new ItemPerformanceResponseDTO
            {
                Range = AnalyticsQueries.ToRangeDto(period),
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
                Filters = catalog.BuildFilters()
            });
        }
    }
}