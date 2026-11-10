using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using back_end.domain;

namespace back_end.Dtos
{
  public class TableAssignmentDto
  {
    [Required(ErrorMessage = "Table Id is Required")]
    [Range(1, int.MaxValue, ErrorMessage = "Table ID Must be Positive")]
    public int TableId { get; set; }
  }

  public class CreateSessionRequestDto
  {
    [Required(ErrorMessage = "Menu Id is Required")]
    [Range(1, int.MaxValue, ErrorMessage = "Menu Id Bust be Positive")]
    public int MenuId { get; set; }
    public int LocationId { get; set; }
    public int? TableId { get; set; }
    public int? TableGroupId { get; set; }

    public string? AssignmentType { get; set; }
  }

  public class DiningSessionResponseDto
  {
    public int SessionId { get; set; }
    public int MenuId { get; set; }
    public int LocationId { get; set; }
    public string? LocationName { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime? FirstOrderTime { get; set; }
    public List<int> TableNumbers { get; set; } = new List<int>();
    public int ActiveParticipants { get; set; }
  }

  public class DiningSessionDetailDto : DiningSessionResponseDto
  {
    public int OrdersCount { get; set; }
    public int BillsCount { get; set; }
    public int TotalParticipant { get; set; }

    public int ActiveBillCount { get; set; }


  }

  public class SessionIdResponseDto
  {
    public int SessionId { get; set; }


  }

  public class SessionMenuResponseDto
  {
    public int MenuId { get; set; }
    public int LocationId { get; set; }
    }

  public class ListDiningSessionsRequestDto
  {
    [FromQuery(Name = "active_only")]
    public bool ActiveOnly { get; set; } = false;
  }

  // Body for POST /api/DiningSession/join. Values come from the table's QR code.
  public class JoinTableSessionDto
  {
    // The number printed on the table, not the database Table_Id
    [Range(1, int.MaxValue, ErrorMessage = "Table number must be positive")]
    public int TableNumber { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Location Id must be positive")]
    public int LocationId { get; set; }
  }

  public class JoinTableSessionResponseDto
  {
    public int SessionId { get; set; }
    public int ParticipantId { get; set; }
    public int MenuId { get; set; }
    public List<int> TableNumbers { get; set; } = new();

    // True when the user was already a participant (rescanning the QR code is safe)
    public bool AlreadyJoined { get; set; }
  }
}
