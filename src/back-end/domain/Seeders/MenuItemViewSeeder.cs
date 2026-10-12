using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.domain.Entities;
using back_end.domain.DbContexts;
using back_end.Helpers;

namespace back_end.domain.Seeders
{
    /// <summary>
    /// Seeds menu_item_view so the Browsing Behavior report has data on a fresh database.
    /// Must run after dining sessions, participants and order items.
    /// </summary>
    /// <remarks>
    /// Per dining session:
    /// - About 80% of the items the session ordered get a 6-60 second view shortly before the order
    ///   (viewed and ordered). The rest have no view (ordered straight from the list).
    /// - 3-10 other items on the session's menu are browsed but not ordered. About 30% of those are
    ///   glances under 5 seconds, which reports ignore.
    /// - Some items are more appealing than others, so "most viewed" and "least viewed" differ.
    /// - About 3% of views are marked as made while the item was unavailable.
    /// All views fall inside the session (start to end, or now for open sessions).
    /// </remarks>
    public class MenuItemViewSeeder : ISeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MenuItemViewSeeder> _logger;
        private readonly Random _rng = new();

        private sealed record SessionInfo(int SessionId, int MenuId, int LocationId, DateTime StartedAt, DateTime? EndedAt);

        public MenuItemViewSeeder(ApplicationDbContext context, ILogger<MenuItemViewSeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        public void Seed()
        {
            var sessions = _context.DiningSessions
                .AsNoTracking()
                .Select(s => new SessionInfo(s.Session_Id, s.Menu_Id, s.Location_Id, s.Started_At, s.Ended_At))
                .ToList();

            if (sessions.Count == 0)
            {
                _logger.LogWarning("Cannot seed menu item views: no dining sessions found");
                return;
            }

            var itemsByMenu = _context.MenuItemAssignments
                .AsNoTracking()
                .Select(a => new { a.Menu_Id, a.Item_Id })
                .ToList()
                .GroupBy(a => a.Menu_Id)
                .ToDictionary(g => g.Key, g => g.Select(a => a.Item_Id).ToList());

            var orderedBySession = _context.OrderItems
                .AsNoTracking()
                .Select(oi => new { oi.Item_Id, SessionId = oi.SessionOrder.session_id, OrderedAt = oi.SessionOrder.Created_At })
                .ToList()
                .GroupBy(o => o.SessionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(o => o.Item_Id).ToDictionary(x => x.Key, x => x.Min(o => o.OrderedAt)));

            var usersBySession = _context.SessionParticipants
                .AsNoTracking()
                .Where(p => p.User_Id != null)
                .Select(p => new { p.Session_Id, p.User_Id })
                .ToList()
                .GroupBy(p => p.Session_Id)
                .ToDictionary(g => g.Key, g => g.Select(p => p.User_Id).ToList());

            var appeal = itemsByMenu.Values
                .SelectMany(ids => ids)
                .Distinct()
                .ToDictionary(id => id, _ => 0.2 + _rng.NextDouble() * 0.8);

            var now = DateTime.UtcNow;
            var views = new List<MenuItemView>();

            foreach (var session in sessions)
            {
                if (!itemsByMenu.TryGetValue(session.MenuId, out var itemsOnMenu) || itemsOnMenu.Count == 0)
                    continue;

                var sessionEnd = session.EndedAt ?? now;
                if (sessionEnd <= session.StartedAt)
                    continue;

                orderedBySession.TryGetValue(session.SessionId, out var orderedItems);
                usersBySession.TryGetValue(session.SessionId, out var users);

                if (orderedItems != null)
                {
                    foreach (var (itemId, orderedAt) in orderedItems)
                    {
                        if (_rng.NextDouble() > 0.8) continue; // ordered without opening the item

                        var seconds = _rng.Next(6, 61);
                        var latestEnd = orderedAt < sessionEnd ? orderedAt : sessionEnd;
                        var latestStart = latestEnd.AddSeconds(-seconds - _rng.Next(10, 121));
                        views.Add(NewView(session, itemId, seconds, Between(session.StartedAt, latestStart), users));
                    }
                }

                var browseCount = _rng.Next(3, 11);
                var browsed = itemsOnMenu
                    .Where(id => orderedItems == null || !orderedItems.ContainsKey(id))
                    .OrderByDescending(id => appeal[id] * _rng.NextDouble())
                    .Take(browseCount);

                foreach (var itemId in browsed)
                {
                    var seconds = _rng.NextDouble() < 0.3 ? _rng.Next(1, 5) : _rng.Next(5, 31);
                    views.Add(NewView(session, itemId, seconds, Between(session.StartedAt, sessionEnd.AddSeconds(-seconds)), users));
                }
            }

            _context.MenuItemViews.AddRange(views);

            var counted = views.Count(v => v.View_Seconds >= ViewTrackingRules.MinQualifyingSeconds);
            _logger.LogInformation(
                "Added {Total} menu item views across {Sessions} sessions ({Counted} of 5+ seconds, {Short} short glances)",
                views.Count, sessions.Count, counted, views.Count - counted);
        }

        private MenuItemView NewView(SessionInfo session, int itemId, int seconds, DateTime viewedAt, List<int?>? users) => new()
        {
            Item_Id = itemId,
            Session_Id = session.SessionId,
            Menu_Id = session.MenuId,
            Location_Id = session.LocationId,
            User_Id = users is { Count: > 0 } ? users[_rng.Next(users.Count)] : null,
            Viewed_At = viewedAt,
            View_Seconds = seconds,
            Was_Available = _rng.NextDouble() >= 0.03
        };

        /// <summary>A random moment between from and to; from when the range is empty.</summary>
        private DateTime Between(DateTime from, DateTime to) =>
            to <= from ? from : from.AddTicks((long)(_rng.NextDouble() * (to - from).Ticks));
    }
}