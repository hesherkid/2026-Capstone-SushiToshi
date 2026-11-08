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
    public int Table_Id { get; set; }
  }

  public class CreateSessionRequestDto
  {
    [JsonPropertyName("menu_id")]
    [Required(ErrorMessage = "Menu Id is Required")]
    [Range(1, int.MaxValue, ErrorMessage = "Menu Id Bust be Positive")]
    public int Menu_Id { get; set; }
    public int Location_Id { get; set; }
    public int? Table_Id { get; set; }
    public int? TableGroup_Id { get; set; }

    public string? AssignmentType { get; set; }
  }

  public class DiningSessionResponseDto
  {
    public int Session_Id { get; set; }
    public int Menu_Id { get; set; }
    public int Location_Id { get; set; }
    public string? Location_Name { get; set; }
    public DateTime Started_at { get; set; }
    public DateTime? Ended_at { get; set; }
    public DateTime? First_Order_Time { get; set; }
    public List<int> Table_Numbers { get; set; } = new List<int>();
    public int Active_Participants { get; set; }
  }

  public class DiningSessionDetailDto : DiningSessionResponseDto
  {
    public int Orders_Count { get; set; }
    public int Bills_Count { get; set; }
    public int Total_participant { get; set; }

    public int Active_Bill_Count { get; set; }


  }

  public class SessionIdResponseDto
  {
    public int Session_Id { get; set; }


  }

  public class SessionMenuResponseDto
  {
    public int Menu_Id { get; set; }
    public int Location_Id { get; set; }
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
    public int Table_Number { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Location Id must be positive")]
    public int Location_Id { get; set; }
  }

  public class JoinTableSessionResponseDto
  {
    public int Session_Id { get; set; }
    public int Participant_Id { get; set; }
    public int Menu_Id { get; set; }
    public List<int> Table_Numbers { get; set; } = new();

    // True when the user was already a participant (rescanning the QR code is safe)
    public bool Already_Joined { get; set; }
  }
}
