

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using back_end.domain.Entities;
using back_end.domain.enums;

namespace back_end.Dtos
{
    public class ServiceRequestCreateDto
    {
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }

    public class ServiceRequestResponseDto
    {
        [JsonPropertyName("request_id")]
        public int RequestId { get; set; }
        [JsonPropertyName("session_id")]
        public int SessionId { get; set; }
        [JsonPropertyName("table_id")]
        public int TableId { get; set; }
        [JsonPropertyName("requested_by")]
        public int RequestedBy { get; set; }
        [JsonPropertyName("claimed_by")]
        public int? ClaimedBy { get; set; }
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
        [JsonPropertyName("status")]
        public ServiceRequestStatus Status { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
        [JsonPropertyName("claimed_at")]
        public DateTime? ClaimedAt { get; set; }
        [JsonPropertyName("completed_at")]
        public DateTime? CompletedAt    { get; set; }
        [JsonPropertyName("table_number")]
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