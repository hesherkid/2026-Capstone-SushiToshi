

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using back_end.domain;
using back_end.domain.Entities;

namespace back_end.Dtos
{
  public class TableEntityBaseDto
  {
    [JsonPropertyName("table_number")]
    [Range(0, int.MaxValue)]
    public int TableNumber { get; set; }
    [JsonPropertyName("seat_count")]
    [Range(0, int.MaxValue)]
    public int SeatCount { get; set; }
    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("qr_code_url")]
    public string? QrCodeUrl { get; set; }

    [JsonPropertyName("location_id")]
    public int LocationId { get; set; }
  }

  public class TableEntityCreateDto : TableEntityBaseDto
  {

  }

  public class TableEntityUpdateDto
  {
    [JsonPropertyName("table_number")]
    [Range(0, int.MaxValue)]
    public int? TableNumber { get; set; }
    [JsonPropertyName("seat_count")]
    [Range(0, int.MaxValue)]
    public int? SeatCount { get; set; }
    [JsonPropertyName("is_active")]
    public bool? IsActive { get; set; }

    [JsonPropertyName("qr_code_url")]
    public string? QrCodeUrl { get; set; }
  }

  public class TableEntityResponseDto : TableEntityBaseDto
  {
    [JsonPropertyName("table_id")]
    public int TableId { get; set; }
    [JsonPropertyName("table_group_id")]
    public int? TableGroupId { get; set; }

    public static TableEntityResponseDto From(TableEntity table) => new()
    {
      TableId = table.Table_Id,
      TableNumber = table.table_number,
      SeatCount = table.seat_count,
      IsActive = table.is_active,
      QrCodeUrl = table.QR_Code,
      LocationId = table.Location_Id,
      TableGroupId = table.TableGroup_Id,
    };
  }

}