using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace back_end.DTO.MenuItemViewDTOs
{
    public class MenuItemViewCreateDTO
    {
        [JsonPropertyName("session_id")]
        [Range(1, int.MaxValue, ErrorMessage = "session_id must be a positive number.")]
        public int SessionId { get; set; }

        [JsonPropertyName("item_id")]
        [Range(1, int.MaxValue, ErrorMessage = "item_id must be a positive number.")]
        public int ItemId { get; set; }

        /// <summary>Whole seconds the item was visible. Values above the cap are stored as the cap.</summary>
        [JsonPropertyName("view_seconds")]
        [Range(0, 86400, ErrorMessage = "view_seconds must be between 0 and 86400.")]
        public int ViewSeconds { get; set; }
    }
}