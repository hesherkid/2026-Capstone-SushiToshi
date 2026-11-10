

using System.ComponentModel.DataAnnotations;
using back_end.domain.Entities;
using back_end.domain.enums;

namespace back_end.Dtos
{
    public class ServiceRequestCreateDto
    {
        public string? Notes { get; set; }
    }

    public class ServiceRequestResponseDto
    {
        public int RequestId { get; set; }
        public int SessionId { get; set; }
        public int TableId { get; set; }
        public int RequestedBy { get; set; }
        public int? ClaimedBy { get; set; }
        public string? Notes { get; set; }
        public ServiceRequestStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ClaimedAt { get; set; }
        public DateTime? CompletedAt    { get; set; }
        public int TableNumber{ get; set; }

        // TableNumber is 0 unless the request's Table is loaded
        public static ServiceRequestResponseDto From(ServiceRequest sr) => new()
        {
            RequestId = sr.request_id,
            SessionId = sr.Session_Id,
            TableId = sr.Table_Id,
            RequestedBy = sr.Request_By,
            ClaimedBy = sr.Claimed_By,
            Notes = sr.Notes,
            Status = sr.Status,
            CreatedAt = sr.Created_At,
            ClaimedAt = sr.Claimed_At,
            CompletedAt = sr.Completed_At,
            TableNumber = sr.Table?.table_number ?? 0,
        };
    }
    
}