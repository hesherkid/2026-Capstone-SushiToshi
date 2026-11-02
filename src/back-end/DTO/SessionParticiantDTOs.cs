using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using back_end.domain;

namespace back_end.DTO.SessionParticipantDTOs
{
    public class SessionParticipantResponseDTO
    {
        [JsonPropertyName("participant")]
        public int Participant_Id { get; set; }
        [JsonPropertyName("session_id")]
        public int Session_Id { get; set; }
        [JsonPropertyName("user_id")]
        public int? User_Id { get; set; }
        [JsonPropertyName("user_name")]
        public string? User_Name { get; set; }
        [JsonPropertyName("joined_at")]
        public DateTime Joined_At { get; set; }
        [JsonPropertyName("Left_At")]
        public DateTime? Left_At { get; set; }
    }

    // A bill at the table, as a guest choosing one sees it: names and party size only, no totals
    public class TableBillChoiceDTO
    {
        [JsonPropertyName("bill_id")]
        public int Bill_Id { get; set; }
        [JsonPropertyName("bill_name")]
        public string Bill_Name { get; set; } = string.Empty;
        [JsonPropertyName("total_count")]
        public int Total_Count { get; set; }
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
        [JsonPropertyName("requested_at")]
        public DateTime? Requested_At { get; set; }
    }

    public class MyBillResponseDTO
    {
        // The bill this guest is on, or null if they still need to pick or start one
        [JsonPropertyName("my_bill")]
        public TableBillChoiceDTO? My_Bill { get; set; }

        // Why they can't move to another bill themselves (staff can), or null if they can
        [JsonPropertyName("change_blocked_reason")]
        public string? Change_Blocked_Reason { get; set; }

        // The table's open bills they could join
        [JsonPropertyName("open_bills")]
        public List<TableBillChoiceDTO> Open_Bills { get; set; } = new();
    }

    public class ChooseBillDTO
    {
        [Required]
        [JsonPropertyName("bill_id")]
        public int Bill_Id { get; set; }
    }
    
}