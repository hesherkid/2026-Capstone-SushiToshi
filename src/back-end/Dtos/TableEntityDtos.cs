

using System.ComponentModel.DataAnnotations;
using back_end.domain;
using back_end.domain.Entities;

namespace back_end.Dtos
{
  public class TableEntityBaseDto
  {
    [Range(0, int.MaxValue)]
    public int TableNumber { get; set; }
    [Range(0, int.MaxValue)]
    public int SeatCount { get; set; }
    public bool IsActive { get; set; } = true;

    public string? QrCodeUrl { get; set; }

    public int LocationId { get; set; }
  }

  public class TableEntityCreateDto : TableEntityBaseDto
  {

  }

  public class TableEntityUpdateDto
  {
    [Range(0, int.MaxValue)]
    public int? TableNumber { get; set; }
    [Range(0, int.MaxValue)]
    public int? SeatCount { get; set; }
    public bool? IsActive { get; set; }

    public string? QrCodeUrl { get; set; }
  }

  public class TableEntityResponseDto : TableEntityBaseDto
  {
    public int TableId { get; set; }
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