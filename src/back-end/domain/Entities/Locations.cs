using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
namespace back_end.domain.Entities
{
  [Table("locations")]

  public class Locations
  {
    [Key]
    public int Location_Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public string Address_Primary { get; set; } = string.Empty;

    public string? Address_Secondary { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Province { get; set; } = string.Empty;

    public string Postal_Code { get; set; } = string.Empty;

    public string Phone_Number { get; set; } = string.Empty;

    public DateTime Created_At { get; set; } = DateTime.UtcNow;

    public DateTime Updated_At { get; set; }

    // Two-letter code used for holidays and the canada-holidays.ca API, e.g. "AB"
    [MaxLength(2)]
    public string Province_Code { get; set; } = "AB";

    // Wi-Fi check: comma-separated networks guests must join a table from, e.g. "203.0.113.10/32".
    // NULL = no check, anyone with the QR link can join. See Helpers/RestaurantNetwork.cs
    [MaxLength(500)]
    public string? Allowed_Networks { get; set; }
    // NULL = this location uses the default pricing profile
    public PricingProfile? PricingProfile { get; set; }

    public ICollection<MenuLocations> MenuLocations { get; set; } = new List<MenuLocations>();
    public ICollection<DiningSession> DiningSessions { get; set; } = new List<DiningSession>();
    public ICollection<TableEntity> Tables { get; set; } = new List<TableEntity>();
    public ICollection<TableGroup> TableGroups { get; set; } = new List<TableGroup>();
    public ICollection<MenuItemAssignment> MenuItemAssignments { get; set; } = new List<MenuItemAssignment>();

    }

}