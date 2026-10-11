namespace back_end.DTO.Analytics
{
    // GET api/analytics/table-turnover.

    public class TableTurnOverResponseDTO
    {
        public List<TableTurnoverDailyDTO> Daily { get; set; } = new List<TableTurnoverDailyDTO>();
        public List<TableTurnOverMonthlyDTO> Monthly { get; set; } = new List<TableTurnOverMonthlyDTO>();
    }

    public class TableTurnoverDailyDTO
    {
        public int Party_size { get; set; }
        public string Day { get; set; } = string.Empty;
        public int AverageDuration { get; set; }
    }

    public class TableTurnOverMonthlyDTO
    {
        public int Party_size { get; set; }
        public string Month { get; set; } = string.Empty;
        public int AverageDuration { get; set; }
    }

    public class TurnoverMetricDTO
    {
        public string? Period { get; set; }
        public int AverageDuration { get; set; }
        public int PartySize { get; set; }
    }
}