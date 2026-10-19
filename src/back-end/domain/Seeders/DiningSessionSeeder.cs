using Microsoft.Extensions.Logging;
using back_end.domain.Entities;
using back_end.domain.DbContexts;
using back_end.Helpers;

namespace back_end.domain.Seeders
{
  /// <summary>
  /// Seeder for dining_sessions table.
  /// Creates mix of active and historical sessions with either individual tables or table groups per location,
  /// plus busy evenings where tables seat several parties in a row. 
  /// </summary>
  public class DiningSessionSeeder : ISeeder
  {
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DiningSessionSeeder> _logger;
    private readonly Random _rng = new();

    public DiningSessionSeeder(ApplicationDbContext context, ILogger<DiningSessionSeeder> logger)
    {
      _context = context;
      _logger = logger;
    }

    public void Seed()
    {
      var locations = _context.Locations.ToList();
      var menus = _context.Menus.ToList();

      if (!locations.Any() || !menus.Any())
      {
        _logger.LogWarning("Cannot seed dining sessions: missing locations or menus");
        return;
      }

      var allSessions = new List<DiningSession>();
      var now = DateTime.UtcNow;

      foreach (var location in locations)
      {
        var tables = _context.Tables
            .Where(t => t.Location_Id == location.Location_Id && t.TableGroup_Id == null)
            .ToList(); // Individual tables not in groups
        var tableGroups = _context.TableGroups
            .Where(tg => tg.Location_Id == location.Location_Id)
            .ToList();

        if (!tables.Any() && !tableGroups.Any())
        {
          _logger.LogWarning($"Cannot seed dining sessions for location {location.Name}: no tables or groups found");
          continue;
        }

        var sessions = new List<DiningSession>();

        // Each table or group gets at most one active session, like the real app enforces.
        // Picking at random with repeats used to seat two sessions at the same table.
        var freeTables = tables.OrderBy(_ => _rng.Next()).ToList();
        var freeGroups = tableGroups.OrderBy(_ => _rng.Next()).ToList();

        // Create up to 10 active sessions (mix of individual tables and groups) per location
        for (int i = 1; i <= 10; i++)
        {
          var startTime = now.AddHours(-_rng.Next(1, 4));
          var menu = menus[_rng.Next(menus.Count)];

          DiningSession session;

          // 70% individual tables, 30% table groups, while free ones remain
          bool useTable = freeTables.Any() && (_rng.NextDouble() < 0.7 || !freeGroups.Any());
          if (useTable)
          {
            var table = freeTables[0];
            freeTables.RemoveAt(0);
            session = new DiningSession
            {
              Menu_Id = menu.Menu_id,
              Location_Id = location.Location_Id,
              Started_At = startTime,
              First_Order_At = startTime.AddMinutes(_rng.Next(5, 20)),
              Ended_At = null,
              Table_Id = table.Table_Id
            };
          }
          else if (freeGroups.Any())
          {
            var group = freeGroups[0];
            freeGroups.RemoveAt(0);
            session = new DiningSession
            {
              Menu_Id = menu.Menu_id,
              Location_Id = location.Location_Id,
              Started_At = startTime,
              First_Order_At = startTime.AddMinutes(_rng.Next(5, 20)),
              Ended_At = null,
              TableGroup_Id = group.TableGroup_Id
            };
          }
          else
          {
            break; // every table and group already has an active session
          }

          sessions.Add(session);
        }

        // Create 40 historical sessions (mix of individual tables and groups) per location
        for (int i = 11; i <= 50; i++)
        {
          var daysAgo = _rng.Next(1, 90);
          var startTime = now.AddDays(-daysAgo).AddHours(_rng.Next(9, 20));
          var duration = _rng.Next(45, 180);
          var menu = menus[_rng.Next(menus.Count)];

          DiningSession session;

          // 70% individual tables, 30% table groups
          if (_rng.NextDouble() < 0.7 && tables.Any())
          {
            var table = tables[_rng.Next(tables.Count)];
            session = new DiningSession
            {
              Menu_Id = menu.Menu_id,
              Location_Id = location.Location_Id,
              Started_At = startTime,
              First_Order_At = startTime.AddMinutes(_rng.Next(5, 20)),
              Ended_At = startTime.AddMinutes(duration),
              Table_Id = table.Table_Id
            };
          }
          else if (tableGroups.Any())
          {
            var group = tableGroups[_rng.Next(tableGroups.Count)];
            session = new DiningSession
            {
              Menu_Id = menu.Menu_id,
              Location_Id = location.Location_Id,
              Started_At = startTime,
              First_Order_At = startTime.AddMinutes(_rng.Next(5, 20)),
              Ended_At = startTime.AddMinutes(duration),
              TableGroup_Id = group.TableGroup_Id
            };
          }
          else
          {
            continue;
          }

          sessions.Add(session);
        }

        sessions.AddRange(CreateTurnoverEvenings(location, tables, tableGroups, menus, now));

        allSessions.AddRange(sessions);


        var activeCount = sessions.Count(s => s.Ended_At == null);
        var historicalCount = sessions.Count(s => s.Ended_At != null);
      }

      _context.DiningSessions.AddRange(allSessions);

      var totalActive = allSessions.Count(s => s.Ended_At == null);
      var totalHistorical = allSessions.Count(s => s.Ended_At != null);
    }
    /// <summary>
    /// Creates busy evenings where the same tables seat two or three parties in a row.
    /// The random historical sessions above are spread over 90 days and many tables, so the
    /// same table almost never has two sessions on one day. Without these evenings the
    /// "time between parties" figures in the Table Turnover report would always be empty.
    /// Each session still gets bills (party size), orders and order items from the later seeders.
    /// </summary>
    private List<DiningSession> CreateTurnoverEvenings(
        Locations location,
        List<TableEntity> tables,
        List<TableGroup> tableGroups,
        List<Menu> menus,
        DateTime now)
    {
      var sessions = new List<DiningSession>();

      var menu = menus.FirstOrDefault(m => m.Name == "All Day Menu") ?? menus[0];

      var timeZone = AnalyticsDateRange.RestaurantTimeZone;
      var today = TimeZoneInfo.ConvertTimeFromUtc(now, timeZone).Date;
      int[] daysAgoList = { 1, 2, 4, 6, 10, 15 };

      for (int evening = 0; evening < daysAgoList.Length; evening++)
      {
        var day = today.AddDays(-daysAgoList[evening]);

        // Single tables: 4 tables, 3 parties each
        var tablesTonight = tables.OrderBy(_ => _rng.Next()).Take(4).ToList();

        for (int t = 0; t < tablesTonight.Count; t++)
        {
          var firstSeatingHour = (evening == 0 && t == 1) ? 15 : 17;
          var seatedAt = day.AddHours(firstSeatingHour).AddMinutes(_rng.Next(0, 31));

          for (int party = 0; party < 3; party++)
          {
            var visitMinutes = _rng.Next(60, 96); // 60-95 minutes, a few over the 90 minute limit
            var leftAt = seatedAt.AddMinutes(visitMinutes);

            sessions.Add(new DiningSession
            {
              Menu_Id = menu.Menu_id,
              Location_Id = location.Location_Id,
              Started_At = ToUtc(seatedAt, timeZone),
              First_Order_At = ToUtc(seatedAt.AddMinutes(_rng.Next(5, 20)), timeZone),
              Ended_At = ToUtc(leftAt, timeZone),
              Table_Id = tablesTonight[t].Table_Id
            });

            seatedAt = leftAt.AddMinutes(GapBeforeNextParty(evening, t, party));
          }
        }

        // One table group: 2 large parties back to back
        if (tableGroups.Any())
        {
          var group = tableGroups[_rng.Next(tableGroups.Count)];
          var seatedAt = day.AddHours(17).AddMinutes(_rng.Next(0, 31));

          for (int party = 0; party < 2; party++)
          {
            var leftAt = seatedAt.AddMinutes(_rng.Next(80, 111)); // big groups stay longer

            sessions.Add(new DiningSession
            {
              Menu_Id = menu.Menu_id,
              Location_Id = location.Location_Id,
              Started_At = ToUtc(seatedAt, timeZone),
              First_Order_At = ToUtc(seatedAt.AddMinutes(_rng.Next(5, 20)), timeZone),
              Ended_At = ToUtc(leftAt, timeZone),
              TableGroup_Id = group.TableGroup_Id
            });

            seatedAt = leftAt.AddMinutes(_rng.Next(10, 31)); // groups take longer to reset
          }
        }
      }

      return sessions;
    }

    /// <summary>
    /// Minutes between one party leaving and the next being seated at the same table.
    /// Mostly 5-25 minutes. On the most recent evening, two tables get special cases
    /// so the report's notes can be shown:
    /// - an overlap: the next party is seated 10 minutes before the last one was closed out
    /// - a long gap: the table sits empty for 2.5 hours (more than the 120 minute cut-off)
    /// </summary>
    private int GapBeforeNextParty(int evening, int table, int party)
    {
      if (evening == 0 && table == 0 && party == 0) return -10;  // overlap
      if (evening == 0 && table == 1 && party == 0) return 150;  // long gap
      return _rng.Next(5, 26);
    }

    private static DateTime ToUtc(DateTime local, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), timeZone);
  }
}