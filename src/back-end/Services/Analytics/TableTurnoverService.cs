using back_end.domain.DbContexts;
using back_end.domain.enums;
using back_end.DTO.Analytics;
using Microsoft.EntityFrameworkCore;

namespace back_end.Services.Analytics
{
    public sealed record TableTurnoverQuery(
        string? Range,
        string? From,
        string? To,
        int? LocationId);

    public interface ITableTurnoverService
    {
        Task<AnalyticsResult<TableTurnoverResponseDTO>> GetAsync(TableTurnoverQuery query);
    }

    public sealed class TableTurnoverService : ITableTurnoverService
    {
        public const int TimeLimitMinutes = 90;
        public const int MaxGapMinutes = 120;
        public const int LargestPartyBucket = 6;
        public const string UnknownPartySize = "unknown";

        private readonly ApplicationDbContext _context;
        private readonly TimeProvider _clock;

        public TableTurnoverService(ApplicationDbContext context, TimeProvider clock)
        {
            _context = context;
            _clock = clock;
        }

        private sealed record SeatedSession(
            int SessionId,
            int? TableId,
            int? TableGroupId,
            DateTime StartedAt,
            DateTime? EndedAt,
            int? PartySize
        );

        public async Task<AnalyticsResult<TableTurnoverResponseDTO>> GetAsync(TableTurnoverQuery query)
        {
            var windowResult = await AnalyticsQueries.ResolveWindowAsync(
                _context, _clock, query.Range, query.From, query.To, query.LocationId);
            if (!windowResult.IsOk) return windowResult.AsError<TableTurnoverResponseDTO>();

            var (period, timeZone, location) = windowResult.Value!;

            var sessions = await LoadSessionsAsync(period.StartUtc, period.EndUtc, query.LocationId);

            var response = new TableTurnoverResponseDTO
            {
                Range = AnalyticsQueries.ToRangeDto(period),
                Location = location
            };

            response.Summary.TimeLimitMinutes = TimeLimitMinutes;
            response.Summary.MaxGapMinutes = MaxGapMinutes;
            response.Summary.SessionsSeated = sessions.Count;
            response.Summary.SessionsOpen = sessions.Count(s => !s.EndedAt.HasValue);
            response.Summary.GuestsSeated = sessions.Sum(s => s.PartySize ?? 0);
            response.Summary.SessionsWithoutPartySize = sessions.Count(s => !s.PartySize.HasValue);
            response.Summary.SessionsOverTimeLimit = sessions.Count(IsOverTimeLimit);

            AddSeatingsByHour(response, sessions, timeZone);
            AddDurationByPartySize(response, sessions);
            AddTurnoverGaps(response, sessions, timeZone);

            return AnalyticsResult<TableTurnoverResponseDTO>.Ok(response);
        }

        private async Task<List<SeatedSession>> LoadSessionsAsync(DateTime startUtc, DateTime endUtc, int? locationId)
        {
            var sessionsQuery = _context.DiningSessions
                .AsNoTracking()
                .Where(s => s.Started_At >= startUtc && s.Started_At < endUtc);

            var billsQuery = _context.Bills
                .AsNoTracking()
                .Where(b => b.Status != BillStatus.Cancelled
                    && b.DiningSession.Started_At >= startUtc
                    && b.DiningSession.Started_At < endUtc);

            if (locationId.HasValue)
            {
                var locId = locationId.Value;
                sessionsQuery = sessionsQuery.Where(s => s.Location_Id == locId);
                billsQuery = billsQuery.Where(b => b.DiningSession.Location_Id == locId);
            }

            var rows = await sessionsQuery
                .Select(s => new { s.Session_Id, s.Table_Id, s.TableGroup_Id, s.Started_At, s.Ended_At })
                .ToListAsync();

            var guestsBySession = (await billsQuery
                    .Select(b => new { b.Session_Id, b.Total_Count })
                    .ToListAsync())
                .GroupBy(b => b.Session_Id)
                .ToDictionary(g => g.Key, g => g.Sum(b => b.Total_Count));

            return rows
                .Select(s => new SeatedSession(
                    s.Session_Id,
                    s.Table_Id,
                    s.TableGroup_Id,
                    s.Started_At,
                    s.Ended_At,
                    guestsBySession.TryGetValue(s.Session_Id, out var guests) && guests > 0 ? guests : null))
                .ToList();
        }

        private static void AddSeatingsByHour(
                    TableTurnoverResponseDTO response, List<SeatedSession> sessions, TimeZoneInfo timeZone)
        {
            var byHour = sessions
                .GroupBy(s => AnalyticsQueries.ToLocal(s.StartedAt, timeZone).Hour)
                .ToDictionary(g => g.Key, g => (Seatings: g.Count(), Guests: g.Sum(s => s.PartySize ?? 0)));

            response.SeatingsByHour = Enumerable.Range(0, 24)
                .Select(hour => new SeatingHourDTO
                {
                    Hour = hour,
                    Label = $"{hour:00}:00",
                    Seatings = byHour.TryGetValue(hour, out var h) ? h.Seatings : 0,
                    Guests = byHour.TryGetValue(hour, out var g) ? g.Guests : 0
                })
                .ToList();

            response.PeakBySeatings = response.SeatingsByHour
                .Where(h => h.Seatings > 0)
                .OrderByDescending(h => h.Seatings)
                .ThenBy(h => h.Hour)
                .FirstOrDefault();

            response.PeakByGuests = response.SeatingsByHour
                .Where(h => h.Guests > 0)
                .OrderByDescending(h => h.Guests)
                .ThenBy(h => h.Hour)
                .FirstOrDefault();
        }

        private static void AddDurationByPartySize(TableTurnoverResponseDTO response, List<SeatedSession> sessions)
        {
            var byBucket = sessions
                .GroupBy(s => PartySizeBucket(s.PartySize))
                .ToDictionary(g => g.Key, g => g.ToList());

            var bucketOrder = Enumerable.Range(1, LargestPartyBucket)
                .Select(size => PartySizeBucket(size))
                .Append(UnknownPartySize);

            response.DurationByPartySize = bucketOrder
                .Where(byBucket.ContainsKey)
                .Select(bucket => new PartySizeDurationDTO
                {
                    PartySize = bucket,
                    Sessions = byBucket[bucket].Count,
                    OverTimeLimit = byBucket[bucket].Count(IsOverTimeLimit),
                    Duration = AnalyticsQueries.Durations(byBucket[bucket]
                        .Where(s => s.EndedAt.HasValue)
                        .Select(s => (s.EndedAt!.Value - s.StartedAt).TotalSeconds))
                })
                .ToList();
        }

        private static void AddTurnoverGaps(
            TableTurnoverResponseDTO response, List<SeatedSession> sessions, TimeZoneInfo timeZone)
        {
            var sessionsByTable = sessions
                 .Where(s => s.TableId != null)
                 .GroupBy(s => s.TableId);

            var sessionsByGroup = sessions
                .Where(s => s.TableGroupId != null)
                .GroupBy(s => s.TableGroupId);

            var tableTally = new GapTally();
            var groupTally = new GapTally();

            foreach (var oneTable in sessionsByTable)
            {
                MeasureGaps(oneTable.ToList(), timeZone, tableTally);
            }

            foreach (var oneGroup in sessionsByGroup)
            {
                MeasureGaps(oneGroup.ToList(), timeZone, groupTally);
            }

            response.TurnoverGap = new TurnoverGapDTO
            {
                Tables = AnalyticsQueries.Durations(tableTally.GapSeconds),
                Groups = AnalyticsQueries.Durations(groupTally.GapSeconds),
                GapsOverLimit = tableTally.OverLimit + groupTally.OverLimit,
                OverlappingSessions = tableTally.Overlapping + groupTally.Overlapping
            };
        }

        private sealed class GapTally
        {
            public List<double> GapSeconds { get; } = new();
            public int OverLimit { get; set; }
            public int Overlapping { get; set; }
        }
        private static void MeasureGaps(List<SeatedSession> sessionsOnOneTable, TimeZoneInfo timeZone, GapTally tally)
        {
            var ordered = sessionsOnOneTable.OrderBy(s => s.StartedAt).ToList();


            for (var i = 1; i < ordered.Count; i++)
            {
                var previous = ordered[i - 1];
                var next = ordered[i];

                if (previous.EndedAt == null)
                {
                    continue;
                }

                var previousEnded = previous.EndedAt.Value;

                var previousEndedDay = AnalyticsQueries.ToLocal(previousEnded, timeZone).Date;
                var nextStartedDay = AnalyticsQueries.ToLocal(next.StartedAt, timeZone).Date;
                if (previousEndedDay != nextStartedDay)
                {
                    continue;
                }

                TimeSpan gap = next.StartedAt - previousEnded;

                if (gap.TotalMinutes < 0)
                {
                    tally.Overlapping++;
                }
                else if (gap.TotalMinutes > MaxGapMinutes)
                {
                    tally.OverLimit++;
                }
                else
                {
                    tally.GapSeconds.Add(gap.TotalSeconds);
                }
            }
        }

        private static string PartySizeBucket(int? partySize) =>
            partySize switch
            {
                null => UnknownPartySize,
                >= LargestPartyBucket => $"{LargestPartyBucket}+",
                _ => partySize.Value.ToString()
            };

        private static bool IsOverTimeLimit(SeatedSession s) =>
            s.EndedAt.HasValue && (s.EndedAt.Value - s.StartedAt).TotalSeconds > TimeLimitMinutes * 60;
    }
}