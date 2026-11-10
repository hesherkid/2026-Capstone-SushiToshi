using back_end.domain.enums;
using System.ComponentModel.DataAnnotations;

namespace back_end.Dtos
{
    public class CreateCategory
    {
        public string? Name { get; set; }

        public string? Description { get; set; }

        [Required(ErrorMessage = "Adult limit is required (0 = unlimited)")]
        public int? AdultLimit { get; set; }

        [Required(ErrorMessage = "Child limit is required (0 = unlimited)")]
        public int? ChildLimit { get; set; }

        [Required(ErrorMessage = "Senior limit is required (0 = unlimited)")]
        public int? SeniorLimit { get; set; }

        public int ToddlerLimit { get; set; }

        public int PerOrderLimit { get; set; }

        public OverLimitBehavior OverLimitBehavior { get; set; } = OverLimitBehavior.Block;
    }

    public class UpdateCategory
    {
        public string? Name { get; set; }

        public string? Description { get; set; }

        public int? AdultLimit { get; set; }

        public int? ChildLimit { get; set; }

        public int? SeniorLimit { get; set; }

        public int? ToddlerLimit { get; set; }

        public int? PerOrderLimit { get; set; }

        public OverLimitBehavior? OverLimitBehavior { get; set; }
    }

    public class CategoryResponse
    {
        public int CategoryId { get; set; }

        public string CategoryName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public int AdultLimit { get; set; }

        public int ChildLimit { get; set; }

        public int SeniorLimit { get; set; }

        public int ToddlerLimit { get; set; }

        public int PerOrderLimit { get; set; }

        public OverLimitBehavior OverLimitBehavior { get; set; }
    }
}