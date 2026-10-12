using Microsoft.Extensions.Logging;
using back_end.domain.Entities;
using back_end.domain.DbContexts;

namespace back_end.domain.Seeders
{
    /// <summary>
    /// Seeder for the locations table with initial data.
    /// </summary>
    public class LocationSeeder : ISeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LocationSeeder> _logger;

        public LocationSeeder(ApplicationDbContext context, ILogger<LocationSeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        public void Seed()
        {
            var locations = new List<Locations>
            {
                new Locations
                {
                    Name = "North Side Branch",
                    Address_Primary = "13619 St Albert Trail NW",
                    City = "Edmonton",
                    Province = "Alberta",
                    Postal_Code = "T5L 5E7",
                    Phone_Number = "780-488-6610",
                    Created_At = new DateTime(2025, 10, 1)
                },
                new Locations
                {
                    Name = "South Side Branch",
                    Address_Primary = "4445 Calgary Trail NW",
                    City = "Edmonton",
                    Province = "Alberta",
                    Postal_Code = "T6H 5R7",
                    Phone_Number = "825-404-6200",
                    Created_At = new DateTime(2025, 10, 1)
                },
            };

            _context.Locations.AddRange(locations);
        }
    }
}
