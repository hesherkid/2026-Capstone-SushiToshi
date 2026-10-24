using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using back_end.domain.enums;

namespace back_end.domain.Entities;

// Holidays belong to a province; a location uses the holidays of its Province_Code
[Table("holidays")]
public class Holiday
{
    [Key]
    public int Holiday_Id { get; set; }

    [Required, MaxLength(2)]
    public string Province_Code { get; set; } = string.Empty;

    // The day holiday pricing applies. DateOnly maps to a MySQL `date`, so no time zone is involved
    public DateOnly Date { get; set; }

    // From canada-holidays.ca, e.g. a Sunday holiday observed on Monday; for reference only
    public DateOnly? Observed_Date { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public HolidaySource Source { get; set; } = HolidaySource.Manual;

    public HolidayStatus Status { get; set; } = HolidayStatus.Pending;

    public DateTime? Imported_At { get; set; }

    [ForeignKey(nameof(ReviewedByUser))]
    public int? Reviewed_By { get; set; }

    public DateTime? Reviewed_At { get; set; }

    public DateTime Created_At { get; set; } = DateTime.UtcNow;

    public User? ReviewedByUser { get; set; }
}
