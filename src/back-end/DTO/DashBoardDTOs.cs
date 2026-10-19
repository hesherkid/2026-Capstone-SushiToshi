using back_end.domain;
using back_end.domain.enums;

namespace back_end.DTO.DashBoardDTOs
{
    public class DashBoardBillDTO
    {
        public int Bill_Id { get; set; }
        public string Bill_Name { get; set; } = string.Empty;
        public BillStatus Status { get; set; }
        public DateTime Created_At { get; set; }
        public int Total_Guests { get; set; }
        public DateTime? Requested_At { get; set; }
    }

    public class DashBoardSessionsDTO
    {
        public int Session_Id { get; set; }
        public int Menu_Id { get; set; }
        public int Location_Id { get; set; }
        public string? Location_Name { get; set; }
        public DateTime Started_At { get; set; }
        public DateTime? Ended_At { get; set; }
        public DateTime? First_Order_At { get; set; }
        public List<int> Table_Numbers { get; set; } = new List<int>();
        public int Active_Participants { get; set; }

        public List<DashBoardBillDTO> Bills { get; set; } = new List<DashBoardBillDTO>();
        public int Bill_Count { get; set; }
        public bool Is_Closable { get; set; }
    }

    public class DashBoardSummaryDTO
    {
        public int? Location_Id { get; set; }
        public string? Location_Name { get; set; }
        public int Total_Active_Sessions { get; set; }
        public int Total_Tables_In_Use { get; set; }
        public int Total_Active_Bills { get; set; }
        public int Total_ActiveParticipants { get; set; }
        
    }
    
}