

using System.ComponentModel.DataAnnotations;
using back_end.domain;
using back_end.domain.Entities;
using back_end.domain.enums;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace back_end.Dtos{
    public class MenuBaseDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public TimeOnly? StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }
        public bool IsActive { get; set; } = true;

    }

    public class MenuCreateDto : MenuBaseDto
    {

    }
    public class MenuUpdateDto
    {
        [MinLength(1)]
        [MaxLength(255)]
        public string? Name { get; set; }
        public string? Description { get; set; }
        public TimeOnly? StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }
        public bool? IsActive { get; set; }
    }

    public class MenuResponseDto : MenuBaseDto
    {
        public int MenuId { get; set; }
        public List<int> LocationIds { get; set; } = [];

        // LocationIds is empty unless the menu's MenuLocations are loaded
        public static MenuResponseDto From(Menu menu) => new()
        {
            MenuId = menu.Menu_id,
            Name = menu.Name,
            Description = menu.Description,
            StartTime = menu.Start_time,
            EndTime = menu.End_time,
            IsActive = menu.Is_active,
            LocationIds = menu.MenuLocations.Select(ml => ml.Location_Id).ToList(),
        };
    }
    
}