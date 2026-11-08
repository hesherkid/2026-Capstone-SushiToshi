using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using back_end.domain.enums;

namespace back_end.domain.Entities
{
  [Table("users")]

  public class User
  {
    [Key]
    public int User_id { get; set; }

    [Required, MaxLength(255), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Normalized_email { get => Email.ToUpper(); private set { } }


    [ForeignKey(nameof(PrimaryLocation))]
    public int? Location_id { get; set; }


    public Locations? PrimaryLocation { get; set; }

    
    [MaxLength(255)]
    public string? Password_hash { get; set; } = string.Empty;
    
    public string? GoogleSocial {get; set;} = string.Empty;

    [Required]
    [MaxLength(100)]
    public string First_name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Last_name { get; set; } = string.Empty;

    [Required]
    public UserRoles Role { get; set; } = UserRoles.Customer;

    public UserStatus Status { get; set; } = UserStatus.Active;

    public DateTime Created_at { get; set; } = DateTime.UtcNow;

    public DateTime? Last_Interaction_at { get; set; }

    public bool Is_email_confirmed { get; set; } = false;


    // Locations an admin has assigned this staff member to (see StaffLocation)
    public ICollection<StaffLocation> AssignedLocations { get; set; } = new List<StaffLocation>();
    public ICollection<SessionOrder> Orders { get; set; } = new List<SessionOrder>();
    public ICollection<SessionParticipant> SessionParticipants { get; set; } = new List<SessionParticipant>();
    public ICollection<ServiceRequest> RequestedServices { get; set; } = new List<ServiceRequest>();
    public ICollection<ServiceRequest> ClaimedServices { get; set; } = new List<ServiceRequest>();

  }
}