using Microsoft.Extensions.Logging;
using back_end.domain.Entities;
using back_end.domain.DbContexts;
using System;
using System.Collections.Generic;

namespace back_end.domain.Seeders
{
    /// <summary>
    /// Seeder for the menu table with initial data.
    /// </summary>
    public class MenuSeeder : ISeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MenuSeeder> _logger;

        public MenuSeeder(ApplicationDbContext context, ILogger<MenuSeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        public void Seed()
        {
            var locations = _context.Locations.ToList();

            var menus = new List<Menu>
            {
                new Menu
                {
                    Name = "All Day Menu",
                    Description = "Our complete selection of dishes available all day",
                    Start_time = new TimeOnly(11, 0),  // 11:00 AM
                    End_time = new TimeOnly(22, 0),    // 10:00 PM
                    Is_active = true
                },
                new Menu
                {
                    Name = "Lunch Special",
                    Description = "Special lunch menu with selected items",
                    Start_time = new TimeOnly(11, 0),  // 11:00 AM
                    End_time = new TimeOnly(15, 0),    // 3:00 PM
                    Is_active = true
                }
            };

            foreach (var menu in menus)
            {
                foreach (var location in locations)
                {
                    menu.MenuLocations.Add(new MenuLocations { Location_Id = location.Location_Id });
                }
            }

            _context.Menus.AddRange(menus);

            _logger.LogInformation($"Added {menus.Count} menus, each served at {locations.Count} locations");

        }
    }
}
