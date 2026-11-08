using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using back_end.domain;

namespace back_end.Dtos
{
    public class SessionParticipantResponseDto
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
    public class TableBillChoiceDto
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
        // How many people at the table are on it, so a guest can see when its count looks short
        [JsonPropertyName("people_count")]
        public int People_Count { get; set; }
    }

    // The guest's own bill: also its counts by kind of guest, so they can update them
    public class MyBillDto : TableBillChoiceDto
    {
        [JsonPropertyName("adult_count")]
        public int Adult_Count { get; set; }
        [JsonPropertyName("senior_count")]
        public int Senior_Count { get; set; }
        [JsonPropertyName("child_count")]
        public int Child_Count { get; set; }
        [JsonPropertyName("tot_count")]
        public int Tot_Count { get; set; }
        // Staff locked the counts after checking them; only staff can change them now
        [JsonPropertyName("counts_locked")]
        public bool Counts_Locked { get; set; }
    }

    public class MyBillResponseDto
    {
        // The bill this guest is on, or null if they still need to pick or start one
        [JsonPropertyName("my_bill")]
        public MyBillDto? My_Bill { get; set; }

        // Why they can't move to another bill themselves (staff can), or null if they can
        [JsonPropertyName("change_blocked_reason")]
        public string? Change_Blocked_Reason { get; set; }

        // The table's open bills they could join
        [JsonPropertyName("open_bills")]
        public List<TableBillChoiceDto> Open_Bills { get; set; } = new();
    }

    public class ChooseBillDto
    {
        [Required]
        [JsonPropertyName("bill_id")]
        public int Bill_Id { get; set; }
    }

    // Staff moving someone at the table onto another bill
    public class MoveGuestDto
    {
        [Required]
        [JsonPropertyName("bill_id")]
        public int Bill_Id { get; set; }

        // Take the orders they placed on their old bill with them
        [JsonPropertyName("move_orders")]
        public bool Move_Orders { get; set; } = true;

        // Take guest_count of this kind of guest off their old bill's count and add them to the new one's,
        // e.g. 4 adults when one phone stands for a party of 4. Null leaves both counts alone.
        [RegularExpression("^(adult|senior|child|toddler)$")]
        [JsonPropertyName("guest_type")]
        public string? Guest_Type { get; set; }

        [Range(1, 50)]
        [JsonPropertyName("guest_count")]
        public int Guest_Count { get; set; } = 1;
    }

    // Staff merging one bill into another at the same table
    public class MergeBillDto
    {
        [Required]
        [JsonPropertyName("into_bill_id")]
        public int Into_Bill_Id { get; set; }
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
        public int Adult_Count { get; set; }
        [Range(0, 50)]
        [JsonPropertyName("senior_count")]
        public int Senior_Count { get; set; }
        [Range(0, 50)]
        [JsonPropertyName("child_count")]
        public int Child_Count { get; set; }
        [Range(0, 50)]
        [JsonPropertyName("tot_count")]
        public int Tot_Count { get; set; }
    }
    
}