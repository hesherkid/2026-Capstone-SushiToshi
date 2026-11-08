

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using back_end.domain;

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

    public string? Qr_Code_Url { get; set; }

    public int Location_Id { get; set; }
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

    public string? Qr_Code_Url { get; set; }
  }

  public class TableEntityResponseDto : TableEntityBaseDto
  {
    [JsonPropertyName("table_id")]
    public int TableId { get; set; }
  }

}