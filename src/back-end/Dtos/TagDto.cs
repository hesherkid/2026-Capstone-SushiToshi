using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace back_end.Dtos
{
  public class TagBaseDto
  {
    [JsonPropertyName("tag_id")]
    public int? Tag_Id { get; set; }

    [JsonPropertyName("tag_name")]
    [Required(ErrorMessage = "Tag name is required")]
    [StringLength(100, ErrorMessage = "Tag name cannot exceed 100 characters")]
    public required string Tag_Name { get; set; }

    [JsonPropertyName("tag_color")]
    [Required(ErrorMessage = "Tag color is required")]
    [StringLength(7, MinimumLength = 7, ErrorMessage = "Tag color must be 7 characters (e.g., #FF5733)")]
    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Tag color must be a valid hex color (e.g., #FF5733)")]
    public required string Tag_Color { get; set; }
  }

  public class TagUpdateDto
  {
    [JsonPropertyName("tag_name")]
    [StringLength(100, ErrorMessage = "Tag name cannot exceed 100 characters")]
    public string? Tag_Name { get; set; }

    [JsonPropertyName("tag_color")]
    [StringLength(7, MinimumLength = 7, ErrorMessage = "Tag color must be 7 characters (e.g., #FF5733)")]
    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Tag color must be a valid hex color (e.g., #FF5733)")]
    public string? Tag_Color { get; set; }
  }
}