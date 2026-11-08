using System.Text.Json.Serialization;
using back_end.domain.Entities;

namespace back_end.Dtos
{
  public class TableGroupAssignmentDto
  {
    public int TableGroup_Id { get; set; }
  }

  public class TableGroupCreateDto
  {
    [JsonPropertyName("group_name")]
    public string GroupName { get; set; } = string.Empty;
    [JsonPropertyName("is_active")]
    public bool? IsActive { get; set; }
    [JsonPropertyName("location_id")]
    public int LocationId { get; set; }
  }

  public class TableGroupUpdateDto
  {
    [JsonPropertyName("group_name")]
    public string? GroupName { get; set; }
    [JsonPropertyName("is_active")]
    public bool? IsActive { get; set; }
  }

  // Needs the group's Tables loaded; LocationName is null unless Location is too
  public class TableGroupResponseDto
  {
    [JsonPropertyName("table_group_id")]
    public int TableGroupId { get; set; }
    [JsonPropertyName("group_name")]
    public string GroupName { get; set; } = string.Empty;
    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
    [JsonPropertyName("location_id")]
    public int LocationId { get; set; }
    [JsonPropertyName("location_name")]
    public string? LocationName { get; set; }
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
    [JsonPropertyName("table_count")]
    public int TableCount { get; set; }
    [JsonPropertyName("total_seats")]
    public int TotalSeats { get; set; }
    [JsonPropertyName("tables")]
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
    [JsonPropertyName("table_id")]
    public int TableId { get; set; }
    [JsonPropertyName("table_number")]
    public int TableNumber { get; set; }
    [JsonPropertyName("seat_count")]
    public int SeatCount { get; set; }
    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
  }
}