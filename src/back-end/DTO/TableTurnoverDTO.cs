using System.Text.Json.Serialization;

namespace back_end.DTO.Analytics
{
    public class TableTurnoverResponseDTO
    {
        [JsonPropertyName("range")]
        public AnalyticsRangeDTO Range { get; set; } = new();

        [JsonPropertyName("location")]
        public AnalyticsLocationDTO? Location { get; set; }

        [JsonPropertyName("summary")]
        public TableTurnoverSummaryDTO Summary { get; set; } = new();

        [JsonPropertyName("peak_by_seatings")]
        public SeatingHourDTO? PeakBySeatings { get; set; }

        [JsonPropertyName("peak_by_guests")]
        public SeatingHourDTO? PeakByGuests { get; set; }

        [JsonPropertyName("seatings_by_hour")]
        public List<SeatingHourDTO> SeatingsByHour { get; set; } = new();

        [JsonPropertyName("duration_by_party_size")]
        public List<PartySizeDurationDTO> DurationByPartySize { get; set; } = new();

        [JsonPropertyName("turnover_gap")]
        public TurnoverGapDTO TurnoverGap { get; set; } = new();

    }

    public class TableTurnoverSummaryDTO
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

    public class SeatingHourDTO
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

    public class PartySizeDurationDTO
    {
        [JsonPropertyName("party_size")]
        public string PartySize { get; set; } = string.Empty;

        [JsonPropertyName("sessions")]
        public int Sessions { get; set; }

        [JsonPropertyName("over_time_limit")]
        public int OverTimeLimit { get; set; }

        [JsonPropertyName("duration")]
        public DurationSummaryDTO Duration { get; set; } = new();
    }

    public class TurnoverGapDTO
    {
        [JsonPropertyName("tables")]
        public DurationSummaryDTO Tables { get; set; } = new();

        [JsonPropertyName("groups")]
        public DurationSummaryDTO Groups { get; set; } = new();

        [JsonPropertyName("gaps_over_limit")]
        public int GapsOverLimit { get; set; }

        [JsonPropertyName("overlapping_sessions")]
        public int OverlappingSessions { get; set; }
    }
}