using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace back_end.domain.Entities
{
  [Table("table_groups")]
  public class TableGroup
  {
    [Key]
    public int TableGroup_Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Group_Name { get; set; } = string.Empty;

    public bool Is_Active { get; set; } = true;

    [ForeignKey(nameof(Location))]
    public int Location_Id { get; set; }

    public DateTime Created_At { get; set; } = DateTime.UtcNow;

    public Locations Location { get; set; } = null!;

    public ICollection<TableEntity> Tables { get; set; } = new List<TableEntity>();
    public ICollection<DiningSession> DiningSessions { get; set; } = new List<DiningSession>();
  }
}
