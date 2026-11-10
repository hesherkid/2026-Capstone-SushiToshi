
namespace back_end.Dtos
{
    // One ended visit on the customer's Past visits page
    public class PastVisitDto
    {
        public int SessionId { get; set; }

        public DateTime StartedAt { get; set; }

        public DateTime? EndedAt { get; set; }

        public string LocationName { get; set; } = string.Empty;

        public List<int> TableNumbers { get; set; } = [];

        // What this customer ordered, same items added together
        public List<PastVisitItemDto> Items { get; set; } = [];

        public List<PastVisitBillDto> Bills { get; set; } = [];
    }

    public class PastVisitItemDto
    {
        public int ItemId { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Quantity { get; set; }
    }

    public class PastVisitBillDto
    {
        public int BillId { get; set; }

        public string BillName { get; set; } = string.Empty;

        public int GuestCount { get; set; }

        // Including gratuity and GST, as on the bill
        public decimal Total { get; set; }
    }
}
