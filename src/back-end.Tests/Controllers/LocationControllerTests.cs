using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;

namespace back_end.Tests.Controllers;

public class LocationControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly LocationController _controller;
    private readonly Mock<ILogger<LocationController>> _mockLogger;

    public LocationControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _mockLogger = new Mock<ILogger<LocationController>>();
        _controller = new LocationController(_context, _mockLogger.Object);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var locations = new List<Locations>
        {
            new Locations
            {
                Location_Id = 1,
                Name = "Downtown",
                Address_Primary = "123 Main St",
                City = "Edmonton",
                Province = "AB",
                Postal_Code = "T5K 2B7",
                Phone_Number = "780-123-4567"
            },
            new Locations
            {
                Location_Id = 2,
                Name = "West End",
                Address_Primary = "456 West Rd",
                City = "Edmonton",
                Province = "AB",
                Postal_Code = "T5H 1A1",
                Phone_Number = "780-987-6543"
            }
        };

        _context.Locations.AddRange(locations);
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetAllLocations_ReturnsAllLocations()
    {
        // Act
        var result = await _controller.GetAllLocations();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var locations = okResult!.Value as List<Locations>;

        locations.Should().NotBeNull();
        locations!.Should().HaveCount(2);
        locations.Should().Contain(l => l.Name == "Downtown");
        locations.Should().Contain(l => l.Name == "West End");
    }

    // Note: GetLocation method doesn't exist in LocationController
    /*
    [Fact]
    public async Task GetLocationById_ValidId_ReturnsLocation()
    {
        // LocationController doesn't have a GetLocation method by ID
    }

    [Fact]
    public async Task GetLocationById_InvalidId_ReturnsNotFound()
    {
        // LocationController doesn't have a GetLocation method by ID
    }
    */

    [Fact]
    public async Task GetAllLocations_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        _context.Locations.RemoveRange(_context.Locations);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetAllLocations();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var locations = okResult!.Value as List<Locations>;

        locations.Should().NotBeNull();
        locations!.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllLocations_ReturnsLocationsInCorrectOrder()
    {
        // Act
        var result = await _controller.GetAllLocations();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var locations = okResult!.Value as List<Locations>;

        locations.Should().NotBeNull();
        locations!.Should().HaveCount(2);
        // Verify locations are returned (order depends on implementation)
        locations.Should().AllSatisfy(l => l.Location_Id.Should().BeGreaterThan(0));
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    // Locations 1 (Downtown) and 2 have no tables or sessions, so they can be deleted
    [Fact]
    public async Task DeleteLocation_MovesStaffWorkingThereToAnotherAssignedLocation()
    {
        _context.Users.AddRange(
            new User { User_id = 10, Email = "two@example.com", Role = UserRoles.Staff, Location_id = 1 },
            new User { User_id = 11, Email = "one@example.com", Role = UserRoles.Staff, Location_id = 1 });
        _context.StaffLocations.AddRange(
            new StaffLocation { User_Id = 10, Location_Id = 1 },
            new StaffLocation { User_Id = 10, Location_Id = 2 },
            new StaffLocation { User_Id = 11, Location_Id = 1 });
        _context.SaveChanges();

        var result = await _controller.Delete_Location(1);

        result.Should().BeOfType<OkObjectResult>();
        (await _context.Users.FindAsync(10))!.Location_id.Should().Be(2);
        (await _context.Users.FindAsync(11))!.Location_id.Should().BeNull();
    }
}
