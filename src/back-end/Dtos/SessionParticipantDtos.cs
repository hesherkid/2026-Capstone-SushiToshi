using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using back_end.domain;

namespace back_end.Dtos
{
    public class SessionParticipantResponseDto
    {
        [JsonPropertyName("participant")]
        public int ParticipantId { get; set; }
        [JsonPropertyName("session_id")]
        public int SessionId { get; set; }
        [JsonPropertyName("user_id")]
        public int? UserId { get; set; }
        [JsonPropertyName("user_name")]
        public string? UserName { get; set; }
        [JsonPropertyName("joined_at")]
        public DateTime JoinedAt { get; set; }
        [JsonPropertyName("Left_At")]
        public DateTime? LeftAt { get; set; }
    }

    // A bill at the table, as a guest choosing one sees it: names and party size only, no totals
    public class TableBillChoiceDto
    {
        [JsonPropertyName("bill_id")]
        public int BillId { get; set; }
        [JsonPropertyName("bill_name")]
        public string BillName { get; set; } = string.Empty;
        [JsonPropertyName("total_count")]
        public int TotalCount { get; set; }
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
        [JsonPropertyName("requested_at")]
        public DateTime? RequestedAt { get; set; }
        // How many people at the table are on it, so a guest can see when its count looks short
        [JsonPropertyName("people_count")]
        public int PeopleCount { get; set; }
    }

    // The guest's own bill: also its counts by kind of guest, so they can update them
    public class MyBillDto : TableBillChoiceDto
    {
        [JsonPropertyName("adult_count")]
        public int AdultCount { get; set; }
        [JsonPropertyName("senior_count")]
        public int SeniorCount { get; set; }
        [JsonPropertyName("child_count")]
        public int ChildCount { get; set; }
        [JsonPropertyName("tot_count")]
        public int TotCount { get; set; }
        // Staff locked the counts after checking them; only staff can change them now
        [JsonPropertyName("counts_locked")]
        public bool CountsLocked { get; set; }
    }

    public class MyBillResponseDto
    {
        // The bill this guest is on, or null if they still need to pick or start one
        [JsonPropertyName("my_bill")]
        public MyBillDto? MyBill { get; set; }

        // Why they can't move to another bill themselves (staff can), or null if they can
        [JsonPropertyName("change_blocked_reason")]
        public string? ChangeBlockedReason { get; set; }

        // The table's open bills they could join
        [JsonPropertyName("open_bills")]
        public List<TableBillChoiceDto> OpenBills { get; set; } = new();
    }

    public class ChooseBillDto
    {
        [Required]
        [JsonPropertyName("bill_id")]
        public int BillId { get; set; }
    }

    // Staff moving someone at the table onto another bill
    public class MoveGuestDto
    {
        [Required]
        [JsonPropertyName("bill_id")]
        public int BillId { get; set; }

        // Take the orders they placed on their old bill with them
        [JsonPropertyName("move_orders")]
        public bool MoveOrders { get; set; } = true;

        // Take guest_count of this kind of guest off their old bill's count and add them to the new one's,
        // e.g. 4 adults when one phone stands for a party of 4. Null leaves both counts alone.
        [RegularExpression("^(adult|senior|child|toddler)$")]
        [JsonPropertyName("guest_type")]
        public string? GuestType { get; set; }

        [Range(1, 50)]
        [JsonPropertyName("guest_count")]
        public int GuestCount { get; set; } = 1;
    }

    // Staff merging one bill into another at the same table
    public class MergeBillDto
    {
        [Required]
        [JsonPropertyName("into_bill_id")]
        public int IntoBillId { get; set; }
    }

    // Staff locking a bill's guest counts so guests can't change them, or unlocking them
    public class LockGuestCountsDto
    {
        [JsonPropertyName("locked")]
        public bool Locked { get; set; }
    }

    // New guest counts for a bill
    public class UpdateGuestCountsDto
    {
        [Range(0, 50)]
        [JsonPropertyName("adult_count")]
        public int AdultCount { get; set; }
        [Range(0, 50)]
        [JsonPropertyName("senior_count")]
        public int SeniorCount { get; set; }
        [Range(0, 50)]
        [JsonPropertyName("child_count")]
        public int ChildCount { get; set; }
        [Range(0, 50)]
        [JsonPropertyName("tot_count")]
        public int TotCount { get; set; }
    }
    
}