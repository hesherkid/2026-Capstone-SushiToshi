using back_end.domain;
using back_end.domain.enums;

namespace back_end.DTO.DashBoardDTOs
{
    public class DashboardBillDto
    {
        public int Bill_Id { get; set; }
        public string Bill_Name { get; set; } = string.Empty;
        public BillStatus Status { get; set; }
        public DateTime Created_At { get; set; }
        public int Total_Guests { get; set; }
        public int Adult_Count { get; set; }
        public int Senior_Count { get; set; }
        public int Child_Count { get; set; }
        public int Tot_Count { get; set; }
        public DateTime? Requested_At { get; set; }
        // Started by a guest on their phone: staff should check its guest counts
        public bool Started_By_Guest { get; set; }
        // A guest changed its guest counts on their phone: staff should check them
        public bool Counts_Changed_By_Guest { get; set; }
        // Staff locked its guest counts, so guests can't change them
        public bool Counts_Locked { get; set; }
        // People at the table on this bill; more than Total_Guests means the count looks short
        public int People_On_Bill { get; set; }
    }

    // Someone seated at the table, and the bill they're on (null: not on one yet)
    public class DashboardPersonDto
    {
        public int Participant_Id { get; set; }
        public string? User_Name { get; set; }
        public int? Bill_Id { get; set; }
    }

    public class DashboardSessionsDto
    {
        public int Session_Id { get; set; }
        public int Menu_Id { get; set; }
        public int Location_Id { get; set; }
        public string? Location_Name { get; set; }
        public DateTime Started_At { get; set; }
        public DateTime? Ended_At { get; set; }
        public DateTime? First_Order_At { get; set; }
        public List<int> Table_Numbers { get; set; } = new List<int>();
        // Seats at the table, or across its group's tables; staff are warned before going past them
        public int Seat_Count { get; set; }
        public int Active_Participants { get; set; }

        public List<DashboardBillDto> Bills { get; set; } = new List<DashboardBillDto>();
        // Everyone still seated, so staff can move them between bills
        public List<DashboardPersonDto> People { get; set; } = new List<DashboardPersonDto>();
        public int Bill_Count { get; set; }
        public bool Is_Closable { get; set; }
    }

    public class DashboardSummaryDto
    {
        public int? Location_Id { get; set; }
        public string? Location_Name { get; set; }
        public int Total_Active_Sessions { get; set; }
        public int Total_Tables_In_Use { get; set; }
        public int Total_Active_Bills { get; set; }
        public int Total_ActiveParticipants { get; set; }
        
    }
    
}