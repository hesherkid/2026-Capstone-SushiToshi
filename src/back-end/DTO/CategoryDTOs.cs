using System.Text.Json.Serialization;
using back_end.domain.enums;
using System.ComponentModel.DataAnnotations;

namespace back_end.DTO.Category
{
    public class CategoryDTOs
    {
        public record CategoryCreateDTO(string Name, string Description);
        public record CategoryUpdateDTO(string Name, string Description);
        public record CategoryReadDTO(int Id, string Name, string Description);
    }

    public class CreateCategory
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Adult limit is required (0 = unlimited)")]
        [JsonPropertyName("adult_limit")]
        public int? Adult_Limit { get; set; }

        [Required(ErrorMessage = "Child limit is required (0 = unlimited)")]
        [JsonPropertyName("child_limit")]
        public int? Child_Limit { get; set; }

        [Required(ErrorMessage = "Senior limit is required (0 = unlimited)")]
        [JsonPropertyName("senior_limit")]
        public int? Senior_Limit { get; set; }

        [JsonPropertyName("toddler_limit")]
        public int Toddler_Limit { get; set; }

        [JsonPropertyName("total_limit")]
        public int Total_Limit { get; set; }

        [JsonPropertyName("over_limit_behavior")]
        public OverLimitBehavior Over_Limit_Behavior { get; set; } = OverLimitBehavior.Block;
    }

    public class UpdateCategory
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("adult_limit")]
        public int? Adult_Limit { get; set; }

        [JsonPropertyName("child_limit")]
        public int? Child_Limit { get; set; }

        [JsonPropertyName("senior_limit")]
        public int? Senior_Limit { get; set; }

        [JsonPropertyName("toddler_limit")]
        public int? Toddler_Limit { get; set; }

        [JsonPropertyName("total_limit")]
        public int? Total_Limit { get; set; }

        [JsonPropertyName("over_limit_behavior")]
        public OverLimitBehavior? Over_Limit_Behavior { get; set; }
    }
}