using System.Text.Json.Serialization;
using back_end.domain.enums;
using System.ComponentModel.DataAnnotations;

namespace back_end.Dtos
{
    public class CreateCategory
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Adult limit is required (0 = unlimited)")]
        [JsonPropertyName("adult_limit")]
        public int? AdultLimit { get; set; }

        [Required(ErrorMessage = "Child limit is required (0 = unlimited)")]
        [JsonPropertyName("child_limit")]
        public int? ChildLimit { get; set; }

        [Required(ErrorMessage = "Senior limit is required (0 = unlimited)")]
        [JsonPropertyName("senior_limit")]
        public int? SeniorLimit { get; set; }

        [JsonPropertyName("toddler_limit")]
        public int ToddlerLimit { get; set; }

        [JsonPropertyName("per_order_limit")]
        public int PerOrderLimit { get; set; }

        [JsonPropertyName("over_limit_behavior")]
        public OverLimitBehavior OverLimitBehavior { get; set; } = OverLimitBehavior.Block;
    }

    public class UpdateCategory
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("adult_limit")]
        public int? AdultLimit { get; set; }

        [JsonPropertyName("child_limit")]
        public int? ChildLimit { get; set; }

        [JsonPropertyName("senior_limit")]
        public int? SeniorLimit { get; set; }

        [JsonPropertyName("toddler_limit")]
        public int? ToddlerLimit { get; set; }

        [JsonPropertyName("per_order_limit")]
        public int? PerOrderLimit { get; set; }

        [JsonPropertyName("over_limit_behavior")]
        public OverLimitBehavior? OverLimitBehavior { get; set; }
    }

    public class CategoryResponse
    {
        [JsonPropertyName("category_id")]
        public int CategoryId { get; set; }

        [JsonPropertyName("category_name")]
        public string CategoryName { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("adult_limit")]
        public int AdultLimit { get; set; }

        [JsonPropertyName("child_limit")]
        public int ChildLimit { get; set; }

        [JsonPropertyName("senior_limit")]
        public int SeniorLimit { get; set; }

        [JsonPropertyName("toddler_limit")]
        public int ToddlerLimit { get; set; }

        [JsonPropertyName("per_order_limit")]
        public int PerOrderLimit { get; set; }

        [JsonPropertyName("over_limit_behavior")]
        public OverLimitBehavior OverLimitBehavior { get; set; }
    }
}