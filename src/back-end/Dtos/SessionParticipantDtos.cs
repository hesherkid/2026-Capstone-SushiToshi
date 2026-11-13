using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using back_end.domain;

namespace back_end.Dtos
{
    public class SessionParticipantResponseDto
    {
        [JsonPropertyName("participant")]
        public int ParticipantId { get; set; }
        public int SessionId { get; set; }
        public int? UserId { get; set; }
        public string? UserName { get; set; }
        public DateTime JoinedAt { get; set; }
        public DateTime? LeftAt { get; set; }
    }

    // A bill at the table, as a guest choosing one sees it: names and party size only, no totals
    public class TableBillChoiceDto
    {
        public int BillId { get; set; }
        public string BillName { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? RequestedAt { get; set; }
        // How many people at the table are on it, so a guest can see when its count looks short
        public int PeopleCount { get; set; }
    }

    // The guest's own bill: also its counts by kind of guest, so they can update them
    public class MyBillDto : TableBillChoiceDto
    {
        public int AdultCount { get; set; }
        public int SeniorCount { get; set; }
        public int ChildCount { get; set; }
        public int TotCount { get; set; }
        // Staff locked the counts after checking them; only staff can change them now
        public bool CountsLocked { get; set; }
    }

    public class MyBillResponseDto
    {
        // The bill this guest is on, or null if they still need to pick or start one
        public MyBillDto? MyBill { get; set; }

        // Why they can't move to another bill themselves (staff can), or null if they can
        public string? ChangeBlockedReason { get; set; }

        // The table's open bills they could join
        public List<TableBillChoiceDto> OpenBills { get; set; } = new();
    }

    public class ChooseBillDto
    {
        [Required]
        public int BillId { get; set; }
    }

    // Staff moving someone at the table onto another bill, or onto a new bill split off their old one
    public class MoveGuestDto
    {
        public int? BillId { get; set; }

        // Start a new bill for them instead of picking an existing one, named after them, with guest_count
        // of guest_type taken off theird old bill's counts.
        public bool NewBill { get; set; }

        // Take the orders they placed on their old bill with them
        public bool MoveOrders { get; set; } = true;

        // Take guest_count of this kind of guest off their old bill's count and add them to the new one's,
        // e.g. 4 adults when one phone stands for a party of 4. Null leaves both counts alone.
        [RegularExpression("^(adult|senior|child|toddler)$")]
        public string? GuestType { get; set; }

        [Range(1, 50)]
        public int GuestCount { get; set; } = 1;
    }

    // Staff merging one bill into another at the same table
    public class MergeBillDto
    {
        [Required]
        public int IntoBillId { get; set; }
    }

    // Staff locking a bill's guest counts so guests can't change them, or unlocking them
    public class LockGuestCountsDto
    {
        public bool Locked { get; set; }
    }

    // New guest counts for a bill
    public class UpdateGuestCountsDto
    {
        [Range(0, 50)]
        public int AdultCount { get; set; }
        [Range(0, 50)]
        public int SeniorCount { get; set; }
        [Range(0, 50)]
        public int ChildCount { get; set; }
        [Range(0, 50)]
        public int TotCount { get; set; }
    }

}