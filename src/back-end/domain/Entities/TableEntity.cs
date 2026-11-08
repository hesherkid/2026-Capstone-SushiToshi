using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace back_end.domain.Entities
{
  [Table("table_entity")]

  public class TableEntity
  {
    [Key]
    public int Table_Id { get; set; }

    [Required]
    public int table_number { get; set; }

    [MaxLength(255)]
    public string QR_Code { get; set; } = string.Empty;

    public int seat_count { get; set; }

    public bool is_active { get; set; } = false;

    [ForeignKey(nameof(Location))]
    public int Location_Id { get; set; }

    [ForeignKey(nameof(TableGroup))]
    public int? TableGroup_Id { get; set; }

    public Locations Location { get; set; } = null!;

    public TableGroup? TableGroup { get; set; }

    public ICollection<DiningSession> DiningSessions { get; set; } = new List<DiningSession>();

    public ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();

  }
}