using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using back_end.domain.enums;

namespace back_end.domain.Entities
{
    [Table("service_request")]
    public class ServiceRequest
    {
        [Key]
        public int request_id { get; set; }

        [ForeignKey(nameof(DiningSession))]
        public int Session_Id { get; set; }

        [ForeignKey(nameof(Table))]
        public int Table_Id { get; set; }

        //[ForeignKey(nameof(RequestedByUser))]
        public int Request_By { get; set; }

        //[ForeignKey(nameof(ClaimedByUser))]
        public int? Claimed_By { get; set; }

        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;

        public ServiceRequestStatus Status { get; set; }

        public DateTime Created_At { get; set; } = DateTime.UtcNow;

        public DateTime? Claimed_At { get; set; }

        public DateTime? Completed_At { get; set; }


        public DiningSession DiningSession { get; set; } = null!;
        public TableEntity Table { get; set; } = null!;
        [ForeignKey("Request_By")]
        public User RequestedByUser { get; set; } = null!;
        [ForeignKey("Claimed_By")]
        public User? ClaimedByUser { get; set; }


    }

}