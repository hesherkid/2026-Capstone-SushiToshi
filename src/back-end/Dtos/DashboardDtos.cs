using back_end.domain;
using back_end.domain.enums;

namespace back_end.Dtos
{
    public class DashboardBillDto
    {
        public int BillId { get; set; }
        public string BillName { get; set; } = string.Empty;
        public BillStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public int TotalGuests { get; set; }
        public int AdultCount { get; set; }
        public int SeniorCount { get; set; }
        public int ChildCount { get; set; }
        public int TotCount { get; set; }
        public DateTime? RequestedAt { get; set; }
        // Started by a guest on their phone: staff should check its guest counts
        public bool StartedByGuest { get; set; }
        // A guest changed its guest counts on their phone: staff should check them
        public bool CountsChangedByGuest { get; set; }
        // Staff locked its guest counts, so guests can't change them
        public bool CountsLocked { get; set; }
        // People at the table on this bill; more than Total_Guests means the count looks short
        public int PeopleOnBill { get; set; }
    }

    // Someone seated at the table, and the bill they're on (null: not on one yet)
    public class DashboardPersonDto
    {
        public int ParticipantId { get; set; }
        public string? UserName { get; set; }
        public int? BillId { get; set; }
        // A "Continue as Guest" account, so staff screens don't use their "Guest K7P2" name for a bill
        public bool IsGuest { get; set; }
    }

    public class DashboardSessionsDto
    {
        public int SessionId { get; set; }
        public int MenuId { get; set; }
        public int LocationId { get; set; }
        public string? LocationName { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public DateTime? FirstOrderAt { get; set; }
        public List<int> TableNumbers { get; set; } = new List<int>();
        // Seats at the table, or across its group's tables; staff are warned before going past them
        public int SeatCount { get; set; }
        public int ActiveParticipants { get; set; }

        public List<DashboardBillDto> Bills { get; set; } = new List<DashboardBillDto>();
        // Everyone still seated, so staff can move them between bills
        public List<DashboardPersonDto> People { get; set; } = new List<DashboardPersonDto>();
        public int BillCount { get; set; }
        public bool IsClosable { get; set; }
    }

    public class DashboardSummaryDto
    {
        public int? LocationId { get; set; }
        public string? LocationName { get; set; }
        public int TotalActiveSessions { get; set; }
        public int TotalTablesInUse { get; set; }
        public int TotalActiveBills { get; set; }
        public int TotalActiveParticipants { get; set; }

    }

}