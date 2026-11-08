

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
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
            
            

    }
    
}