

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using back_end.domain.enums;

namespace back_end.DTO.ServiceRequestDTOs
{
    public class ServiceRequestCreateDto
    {
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }

    public class ServiceRequestResponseDto
    {
        [JsonPropertyName("request_id")]
        public int Request_Id { get; set; }
        [JsonPropertyName("session_id")]
        public int Session_Id { get; set; }
        [JsonPropertyName("table_id")]
        public int Table_Id { get; set; }
        [JsonPropertyName("requested_by")]
        public int Requested_By { get; set; }
        [JsonPropertyName("claimed_by")]
        public int? Claimed_By { get; set; }
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
        [JsonPropertyName("status")]
        public ServiceRequestStatus Status { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime Created_At { get; set; }
        [JsonPropertyName("claimed_at")]
        public DateTime? Claimed_At { get; set; }
        [JsonPropertyName("completed_at")]
        public DateTime? Completed_at    { get; set; }
        [JsonPropertyName("table_number")]
        public int Table_Number{ get; set; }
            
            

    }
    
}