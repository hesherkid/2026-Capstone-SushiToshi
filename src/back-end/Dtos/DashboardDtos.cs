using System.Text.Json.Serialization;
using back_end.domain;
using back_end.domain.enums;

namespace back_end.Dtos
{
    public class DashboardBillDto
    {
        [JsonPropertyName("bill_id")]
        public int BillId { get; set; }
        [JsonPropertyName("bill_name")]
        public string BillName { get; set; } = string.Empty;
        public BillStatus Status { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
        [JsonPropertyName("total_guests")]
        public int TotalGuests { get; set; }
        [JsonPropertyName("adult_count")]
        public int AdultCount { get; set; }
        [JsonPropertyName("senior_count")]
        public int SeniorCount { get; set; }
        [JsonPropertyName("child_count")]
        public int ChildCount { get; set; }
        [JsonPropertyName("tot_count")]
        public int TotCount { get; set; }
        [JsonPropertyName("requested_at")]
        public DateTime? RequestedAt { get; set; }
        // Started by a guest on their phone: staff should check its guest counts
        [JsonPropertyName("started_by_guest")]
        public bool StartedByGuest { get; set; }
        // A guest changed its guest counts on their phone: staff should check them
        [JsonPropertyName("counts_changed_by_guest")]
        public bool CountsChangedByGuest { get; set; }
        // Staff locked its guest counts, so guests can't change them
        [JsonPropertyName("counts_locked")]
        public bool CountsLocked { get; set; }
        // People at the table on this bill; more than Total_Guests means the count looks short
        [JsonPropertyName("people_on_bill")]
        public int PeopleOnBill { get; set; }
    }

    // Someone seated at the table, and the bill they're on (null: not on one yet)
    public class DashboardPersonDto
    {
        [JsonPropertyName("participant_id")]
        public int ParticipantId { get; set; }
        [JsonPropertyName("user_name")]
        public string? UserName { get; set; }
        [JsonPropertyName("bill_id")]
        public int? BillId { get; set; }
    }

    public class DashboardSessionsDto
    {
        [JsonPropertyName("session_id")]
        public int SessionId { get; set; }
        [JsonPropertyName("menu_id")]
        public int MenuId { get; set; }
        [JsonPropertyName("location_id")]
        public int LocationId { get; set; }
        [JsonPropertyName("location_name")]
        public string? LocationName { get; set; }
        [JsonPropertyName("started_at")]
        public DateTime StartedAt { get; set; }
        [JsonPropertyName("ended_at")]
        public DateTime? EndedAt { get; set; }
        [JsonPropertyName("first_order_at")]
        public DateTime? FirstOrderAt { get; set; }
        [JsonPropertyName("table_numbers")]
        public List<int> TableNumbers { get; set; } = new List<int>();
        // Seats at the table, or across its group's tables; staff are warned before going past them
        [JsonPropertyName("seat_count")]
        public int SeatCount { get; set; }
        [JsonPropertyName("active_participants")]
        public int ActiveParticipants { get; set; }

        public List<DashboardBillDto> Bills { get; set; } = new List<DashboardBillDto>();
        // Everyone still seated, so staff can move them between bills
        public List<DashboardPersonDto> People { get; set; } = new List<DashboardPersonDto>();
        [JsonPropertyName("bill_count")]
        public int BillCount { get; set; }
        [JsonPropertyName("is_closable")]
        public bool IsClosable { get; set; }
    }

    public class DashboardSummaryDto
    {
        [JsonPropertyName("location_id")]
        public int? LocationId { get; set; }
        [JsonPropertyName("location_name")]
        public string? LocationName { get; set; }
        [JsonPropertyName("total_active_sessions")]
        public int TotalActiveSessions { get; set; }
        [JsonPropertyName("total_tables_in_use")]
        public int TotalTablesInUse { get; set; }
        [JsonPropertyName("total_active_bills")]
        public int TotalActiveBills { get; set; }
        [JsonPropertyName("total_active_participants")]
        public int TotalActiveParticipants { get; set; }
        
    }
    
}