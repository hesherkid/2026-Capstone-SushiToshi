namespace back_end.DTO.Analytics
{
    // GET api/analytics/order-timing.

    public class OrderTimingResponseDTO
    {
        public List<OrderTimingDTO> OrderTimings { get; set; } = new List<OrderTimingDTO>();
        public List<DailyAverageTimingDTO> DailyAverages { get; set; } = new List<DailyAverageTimingDTO>();
    }

    public class OrderTimingDTO
    {
        public int SessionId { get; set; }
        public int TimeToFirstOrder { get; set; }
    }

    public class DailyAverageTimingDTO
    {
        public string Day { get; set; } = string.Empty;
        public double AverageTimeToFirstOrder { get; set; }
    }
}