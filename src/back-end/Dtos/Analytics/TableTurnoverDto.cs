
namespace back_end.Dtos.Analytics
{
    public class TableTurnoverResponseDto
    {
        public AnalyticsRangeDto Range { get; set; } = new();

        public AnalyticsLocationDto? Location { get; set; }

        public TableTurnoverSummaryDto Summary { get; set; } = new();

        public SeatingHourDto? PeakBySeatings { get; set; }

        public SeatingHourDto? PeakByGuests { get; set; }

        public List<SeatingHourDto> SeatingsByHour { get; set; } = new();

        public List<PartySizeDurationDto> DurationByPartySize { get; set; } = new();

        public TurnoverGapDto TurnoverGap { get; set; } = new();

    }

    public class TableTurnoverSummaryDto
    {
        public int SessionsSeated { get; set; }

        public int SessionsOpen { get; set; }

        public int GuestsSeated { get; set; }

        public int SessionsWithoutPartySize { get; set; }

        public int SessionsOverTimeLimit { get; set; }

        public int TimeLimitMinutes { get; set; }

        public int MaxGapMinutes { get; set; }
    }

    public class SeatingHourDto
    {
        public int Hour { get; set; }

        public string Label { get; set; } = string.Empty;

        public int Seatings { get; set; }

        public int Guests { get; set; }
    }

    public class PartySizeDurationDto
    {
        public string PartySize { get; set; } = string.Empty;

        public int Sessions { get; set; }

        public int OverTimeLimit { get; set; }

        public DurationSummaryDto Duration { get; set; } = new();
    }

    public class TurnoverGapDto
    {
        public DurationSummaryDto Tables { get; set; } = new();

        public DurationSummaryDto Groups { get; set; } = new();

        public int GapsOverLimit { get; set; }

        public int OverlappingSessions { get; set; }
    }
}