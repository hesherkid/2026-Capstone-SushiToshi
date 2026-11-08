using System.Text.Json.Serialization;

namespace back_end.Dtos.Analytics
{
    public class TableTurnoverResponseDto
    {
        [JsonPropertyName("range")]
        public AnalyticsRangeDto Range { get; set; } = new();

        [JsonPropertyName("location")]
        public AnalyticsLocationDto? Location { get; set; }

        [JsonPropertyName("summary")]
        public TableTurnoverSummaryDto Summary { get; set; } = new();

        [JsonPropertyName("peak_by_seatings")]
        public SeatingHourDto? PeakBySeatings { get; set; }

        [JsonPropertyName("peak_by_guests")]
        public SeatingHourDto? PeakByGuests { get; set; }

        [JsonPropertyName("seatings_by_hour")]
        public List<SeatingHourDto> SeatingsByHour { get; set; } = new();

        [JsonPropertyName("duration_by_party_size")]
        public List<PartySizeDurationDto> DurationByPartySize { get; set; } = new();

        [JsonPropertyName("turnover_gap")]
        public TurnoverGapDto TurnoverGap { get; set; } = new();

    }

    public class TableTurnoverSummaryDto
    {
        [JsonPropertyName("sessions_seated")]
        public int SessionsSeated { get; set; }

        [JsonPropertyName("sessions_open")]
        public int SessionsOpen { get; set; }

        [JsonPropertyName("guests_seated")]
        public int GuestsSeated { get; set; }

        [JsonPropertyName("sessions_without_party_size")]
        public int SessionsWithoutPartySize { get; set; }

        [JsonPropertyName("sessions_over_time_limit")]
        public int SessionsOverTimeLimit { get; set; }

        [JsonPropertyName("time_limit_minutes")]
        public int TimeLimitMinutes { get; set; }

        [JsonPropertyName("max_gap_minutes")]
        public int MaxGapMinutes { get; set; }
    }

    public class SeatingHourDto
    {
        [JsonPropertyName("hour")]
        public int Hour { get; set; }

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("seatings")]
        public int Seatings { get; set; }

        [JsonPropertyName("guests")]
        public int Guests { get; set; }
    }

    public class PartySizeDurationDto
    {
        [JsonPropertyName("party_size")]
        public string PartySize { get; set; } = string.Empty;

        [JsonPropertyName("sessions")]
        public int Sessions { get; set; }

        [JsonPropertyName("over_time_limit")]
        public int OverTimeLimit { get; set; }

        [JsonPropertyName("duration")]
        public DurationSummaryDto Duration { get; set; } = new();
    }

    public class TurnoverGapDto
    {
        [JsonPropertyName("tables")]
        public DurationSummaryDto Tables { get; set; } = new();

        [JsonPropertyName("groups")]
        public DurationSummaryDto Groups { get; set; } = new();

        [JsonPropertyName("gaps_over_limit")]
        public int GapsOverLimit { get; set; }

        [JsonPropertyName("overlapping_sessions")]
        public int OverlappingSessions { get; set; }
    }
}