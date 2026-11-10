using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using back_end.domain;

namespace back_end.Dtos
{
  public class TableAssignmentDto
  {
    [JsonPropertyName("table_id")]
    [Required(ErrorMessage = "Table Id is Required")]
    [Range(1, int.MaxValue, ErrorMessage = "Table ID Must be Positive")]
    public int TableId { get; set; }
  }

  public class CreateSessionRequestDto
  {
    [JsonPropertyName("menu_id")]
    [Required(ErrorMessage = "Menu Id is Required")]
    [Range(1, int.MaxValue, ErrorMessage = "Menu Id Bust be Positive")]
    public int MenuId { get; set; }
    [JsonPropertyName("location_id")]
    public int LocationId { get; set; }
    [JsonPropertyName("table_id")]
    public int? TableId { get; set; }
    [JsonPropertyName("table_group_id")]
    public int? TableGroupId { get; set; }

    [JsonPropertyName("assignment_type")]
    public string? AssignmentType { get; set; }
  }

  public class DiningSessionResponseDto
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
    [JsonPropertyName("first_order_time")]
    public DateTime? FirstOrderTime { get; set; }
    [JsonPropertyName("table_numbers")]
    public List<int> TableNumbers { get; set; } = new List<int>();
    [JsonPropertyName("active_participants")]
    public int ActiveParticipants { get; set; }
  }

  public class DiningSessionDetailDto : DiningSessionResponseDto
  {
    [JsonPropertyName("orders_count")]
    public int OrdersCount { get; set; }
    [JsonPropertyName("bills_count")]
    public int BillsCount { get; set; }
    [JsonPropertyName("total_participant")]
    public int TotalParticipant { get; set; }

    [JsonPropertyName("active_bill_count")]
    public int ActiveBillCount { get; set; }


  }

  public class SessionIdResponseDto
  {
    [JsonPropertyName("session_id")]
    public int SessionId { get; set; }


  }

  public class SessionMenuResponseDto
  {
    [JsonPropertyName("menu_id")]
    public int MenuId { get; set; }
    [JsonPropertyName("location_id")]
    public int LocationId { get; set; }
    }

  public class ListDiningSessionsRequestDto
  {
    public bool ActiveOnly { get; set; } = false;
  }

  // Body for POST /api/DiningSession/join. Values come from the table's QR code.
  public class JoinTableSessionDto
  {
    // The number printed on the table, not the database Table_Id
    [Range(1, int.MaxValue, ErrorMessage = "Table number must be positive")]
    [JsonPropertyName("table_number")]
    public int TableNumber { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Location Id must be positive")]
    [JsonPropertyName("location_id")]
    public int LocationId { get; set; }
  }

  public class JoinTableSessionResponseDto
  {
    [JsonPropertyName("session_id")]
    public int SessionId { get; set; }
    [JsonPropertyName("participant_id")]
    public int ParticipantId { get; set; }
    [JsonPropertyName("menu_id")]
    public int MenuId { get; set; }
    [JsonPropertyName("table_numbers")]
    public List<int> TableNumbers { get; set; } = new();

    // True when the user was already a participant (rescanning the QR code is safe)
    [JsonPropertyName("already_joined")]
    public bool AlreadyJoined { get; set; }
  }
}
