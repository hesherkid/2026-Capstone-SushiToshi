using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;
namespace back_end.domain.Entities
{
    [Table("session_participant")]
    public class SessionParticipant
    {
        [Key]
        public int Participant_Id { get; set; }

        [ForeignKey(nameof(DiningSession))]
        public int Session_Id { get; set; }
        
        [ForeignKey(nameof(User))]
        public int? User_Id { get; set; }

        [MaxLength(200)]
        public string? User_Name { get; set; }

        public DateTime Joined_At { get; set; } = DateTime.UtcNow;

        public DateTime? Left_At { get; set; }


        public DiningSession DiningSession { get; set; } = null!;
        public User? User { get; set; } = null!;
        
    }

}