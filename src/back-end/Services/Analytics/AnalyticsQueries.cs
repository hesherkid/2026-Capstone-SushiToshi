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

        /// <summary>Restaurant time zone for day boundaries. Configure "Analytics:TimeZoneId" in appsettings if different from the server's default time zone.</summary>
        public static TimeZoneInfo GetTimeZone(IConfiguration config)
        {
            var id = config["Analytics:TimeZoneId"];
            return TimeZoneInfo.FindSystemTimeZoneById(
                string.IsNullOrWhiteSpace(id) ? AnalyticsDateRange.DefaultTimeZoneId : id);
        }

        public static AnalyticsResult<T>? ValidateTop<T>(int top) =>
            top < MinTop || top > MaxTop
                ? AnalyticsResult<T>.Invalid("top", $"top must be between {MinTop} and {MaxTop}")
                : null;

        /// <summary>
        /// Resolves the date range and checks the location exists.
        /// Throws if the configured time zone is unknown (a server problem, reported as 500).
        /// </summary>
        public static async Task<AnalyticsResult<AnalyticsWindow>> ResolveWindowAsync(
            ApplicationDbContext context,
            IConfiguration config,
            TimeProvider clock,
            string? range,
            string? from,
            string? to,
            int? locationId)
        {
            var timeZone = GetTimeZone(config);

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

        /// <summary>
        /// Items to report: everything in <paramref name="alwaysInclude"/> (items with activity in the
        /// window, even if unavailable or off the menu now) plus everything orderable right now (not
        /// Unavailable, on an active menu, at the location when given). Items that are unavailable now 
        /// and had no activity are left out, because availability history isn't stored.
        /// </summary>
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

            // Tags in a separate query; simpler SQL than a nested collection.
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

        /// <summary>part / whole as a percent to 1 decimal place. Null when whole is 0.</summary>
        public static decimal? Percent(int part, int whole) =>
            whole == 0 ? null : Math.Round(part * 100m / whole, 1, MidpointRounding.AwayFromZero);

        /// <summary>Seconds per view to 1 decimal place. Null when there are no views.</summary>
        public static decimal? SecondsPerView(int seconds, int views) =>
            views == 0 ? null : Math.Round((decimal)seconds / views, 1, MidpointRounding.AwayFromZero);
    }
}