using System.Text.Json.Serialization;

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

        [JsonPropertyName("adult_limit")]
        public int Adult_Limit { get; set; }

        [JsonPropertyName("child_limit")]
        public int Child_Limit { get; set; }

        [JsonPropertyName("senior_limit")]
        public int Senior_Limit { get; set; }

        [JsonPropertyName("toddler_limit")]
        public int Toddler_Limit { get; set; }

        [JsonPropertyName("total_limit")]
        public int Total_Limit { get; set; }
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
    }
}