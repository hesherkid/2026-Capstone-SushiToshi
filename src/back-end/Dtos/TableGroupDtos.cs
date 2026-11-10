using back_end.domain.Entities;

namespace back_end.Dtos
{
  public class TableGroupAssignmentDto
  {
    public int TableGroupId { get; set; }
  }

  public class TableGroupCreateDto
  {
    public string GroupName { get; set; } = string.Empty;
    public bool? IsActive { get; set; }
    public int LocationId { get; set; }
  }

  public class TableGroupUpdateDto
  {
    public string? GroupName { get; set; }
    public bool? IsActive { get; set; }
  }

  // Needs the group's Tables loaded; LocationName is null unless Location is too
  public class TableGroupResponseDto
  {
    public int TableGroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int LocationId { get; set; }
    public string? LocationName { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TableCount { get; set; }
    public int TotalSeats { get; set; }
    public List<TableGroupTableDto> Tables { get; set; } = [];

    public static TableGroupResponseDto From(TableGroup group) => new()
    {
      TableGroupId = group.TableGroup_Id,
      GroupName = group.Group_Name,
      IsActive = group.Is_Active,
      LocationId = group.Location_Id,
      LocationName = group.Location?.Name,
      CreatedAt = group.Created_At,
      TableCount = group.Tables.Count,
      TotalSeats = group.Tables.Sum(t => t.seat_count),
      Tables = group.Tables.Select(t => new TableGroupTableDto
      {
        TableId = t.Table_Id,
        TableNumber = t.table_number,
        SeatCount = t.seat_count,
        IsActive = t.is_active,
      }).ToList(),
    };
  }

  public class TableGroupTableDto
  {
    public int TableId { get; set; }
    public int TableNumber { get; set; }
    public int SeatCount { get; set; }
    public bool IsActive { get; set; }
  }
}