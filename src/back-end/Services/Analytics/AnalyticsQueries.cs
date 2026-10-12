using back_end.domain.DbContexts;
using back_end.domain.enums;
using back_end.DTO.Analytics;
using back_end.Helpers;
using Microsoft.EntityFrameworkCore;

namespace back_end.Services.Analytics
{
    internal sealed record AnalyticsWindow(
        ResolvedDateRange Period,
        TimeZoneInfo TimeZone,
        AnalyticsLocationDTO? Location);

    internal sealed record CandidateItem(
        int ItemId,
        string Name,
        int CategoryId,
        string CategoryName,
        MenuItemStatus Status,
        bool CurrentlyAvailable);

    internal sealed class ItemCatalog
    {
        private readonly Dictionary<int, List<AnalyticsTagDTO>> _tagsByItem;

        public ItemCatalog(List<CandidateItem> items, Dictionary<int, List<AnalyticsTagDTO>> tagsByItem)
        {
            Items = items;
            _tagsByItem = tagsByItem;
        }

        public IReadOnlyList<CandidateItem> Items { get; }

        public List<AnalyticsTagDTO> TagsFor(int itemId) =>
            _tagsByItem.TryGetValue(itemId, out var tags) ? tags : new List<AnalyticsTagDTO>();

        public IEnumerable<CandidateItem> Filter(int? categoryId, int? tagId) =>
            Items
                .Where(i => categoryId == null || i.CategoryId == categoryId)
                .Where(i => tagId == null || TagsFor(i.ItemId).Any(t => t.TagId == tagId));

        public AnalyticsFiltersDTO BuildFilters() => new()
        {
            Categories = Items
                .GroupBy(i => new { i.CategoryId, i.CategoryName })
                .Select(g => new AnalyticsFilterOptionDTO { Id = g.Key.CategoryId, Name = g.Key.CategoryName })
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Tags = _tagsByItem.Values
                .SelectMany(t => t)
                .GroupBy(t => t.TagId)
                .Select(g => g.First())
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    internal static class AnalyticsQueries
    {
        public const int MinTop = 1;
        public const int MaxTop = 25;

        public static AnalyticsResult<T>? ValidateTop<T>(int top) =>
            top < MinTop || top > MaxTop
                ? AnalyticsResult<T>.Invalid("top", $"top must be between {MinTop} and {MaxTop}")
                : null;

        public static async Task<AnalyticsResult<AnalyticsWindow>> ResolveWindowAsync(
            ApplicationDbContext context,
            TimeProvider clock,
            string? range,
            string? from,
            string? to,
            int? locationId)
        {
            var timeZone = AnalyticsDateRange.RestaurantTimeZone;

            if (!AnalyticsDateRange.TryResolve(range, from, to, clock.GetUtcNow().UtcDateTime, timeZone,
                    out var period, out var errorKey, out var errorMessage) || period == null)
            {
                return AnalyticsResult<AnalyticsWindow>.Invalid(errorKey, errorMessage);
            }

            AnalyticsLocationDTO? location = null;
            if (locationId.HasValue)
            {
                var locId = locationId.Value;
                location = await context.Locations
                    .AsNoTracking()
                    .Where(l => l.Location_Id == locId)
                    .Select(l => new AnalyticsLocationDTO { Id = l.Location_Id, Name = l.Name })
                    .FirstOrDefaultAsync();

                if (location == null)
                {
                    return AnalyticsResult<AnalyticsWindow>.NotFound($"Location {locId} was not found.");
                }
            }

            return AnalyticsResult<AnalyticsWindow>.Ok(new AnalyticsWindow(period, timeZone, location));
        }

        public static async Task<ItemCatalog> LoadCatalogAsync(
            ApplicationDbContext context,
            int? locationId,
            IEnumerable<int> alwaysInclude)
        {
            var includeIds = alwaysInclude.Distinct().ToList();

            var candidates = await context.MenuItems
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
                .Where(x => x.CurrentlyAvailable || includeIds.Contains(x.item_id))
                .ToListAsync();

            var items = candidates
                .Select(x => new CandidateItem(x.item_id, x.Name, x.Category_id, x.CategoryName, x.Status, x.CurrentlyAvailable))
                .ToList();

            var candidateIds = items.Select(i => i.ItemId).ToList();
            var tagsByItem = (await context.MenuItemTags
                    .AsNoTracking()
                    .Where(t => candidateIds.Contains(t.Menu_item_id))
                    .Select(t => new
                    {
                        t.Menu_item_id,
                        Tag = new AnalyticsTagDTO
                        {
                            TagId = t.Tag_id,
                            Name = t.Tag.tag_name,
                            Color = t.Tag.tag_color
                        }
                    })
                    .ToListAsync())
                .GroupBy(t => t.Menu_item_id)
                .ToDictionary(g => g.Key, g => g.Select(t => t.Tag).OrderBy(t => t.Name).ToList());

            return new ItemCatalog(items, tagsByItem);
        }

        public static AnalyticsRangeDTO ToRangeDto(ResolvedDateRange period) => new()
        {
            Preset = period.Preset.ToString().ToLowerInvariant(),
            From = period.FromLocal.ToString("yyyy-MM-dd"),
            To = period.ToLocal.ToString("yyyy-MM-dd"),
            Days = period.ToLocal.DayNumber - period.FromLocal.DayNumber + 1,
            TimeZone = period.TimeZoneId
        };

        public static decimal? Percent(int part, int whole) =>
            whole == 0 ? null : Math.Round(part * 100m / whole, 1, MidpointRounding.AwayFromZero);

        public static decimal? SecondsPerView(int seconds, int views) =>
            views == 0 ? null : Math.Round((decimal)seconds / views, 1, MidpointRounding.AwayFromZero);

        public static DurationSummaryDTO Durations(IEnumerable<double> seconds)
        {
            var sorted = seconds.Where(s => s >= 0).OrderBy(s => s).ToList();
            if (sorted.Count == 0) return new DurationSummaryDTO();

            var mid = sorted.Count / 2;
            var median = sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;

            return new DurationSummaryDTO
            {
                Count = sorted.Count,
                AverageMinutes = ToMinutes(sorted.Average()),
                MedianMinutes = ToMinutes(median)
            };
        }

        public static DateTime ToLocal(DateTime utc, TimeZoneInfo timeZone) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone);

        private static decimal ToMinutes(double seconds) =>
            Math.Round((decimal)seconds / 60m, 1, MidpointRounding.AwayFromZero);
    }
}