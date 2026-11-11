using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using Microsoft.AspNetCore.Http;

namespace back_end.Tests.Controllers;

public class MenuLocationControllerTests : IDisposable
{
    private const int Dinner = 1, Lunch = 2, North = 1, South = 2, East = 3, Missing = 99;

    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<MenuLocationController>> _logger = new();
    private readonly MenuLocationController _controller;

    public MenuLocationControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _controller = new MenuLocationController(_context, _logger.Object, Mock.Of<IWebHostEnvironment>());

        _context.Menus.AddRange(
            new Menu { Menu_id = Dinner, Name = "Dinner", Description = "Dinner", Is_active = true },
            new Menu { Menu_id = Lunch, Name = "Lunch", Description = "Lunch", Is_active = true });
        _context.Locations.AddRange(
            new Locations { Location_Id = North, Name = "North", Address_Primary = "1 North St", City = "Edmonton", Province = "AB", Postal_Code = "T5K 2B7", Phone_Number = "780-555-0001" },
            new Locations { Location_Id = South, Name = "South", Address_Primary = "2 South St", City = "Edmonton", Province = "AB", Postal_Code = "T6X 1A1", Phone_Number = "780-555-0002" },
            new Locations { Location_Id = East, Name = "East", Address_Primary = "3 East St", City = "Edmonton", Province = "AB", Postal_Code = "T5A 0A1", Phone_Number = "780-555-0003" });
        // Dinner runs at North and South; Lunch runs at South only; East has no menus yet
        _context.MenuLocations.AddRange(
            new MenuLocations { Menu_Id = Dinner, Location_Id = North },
            new MenuLocations { Menu_Id = Dinner, Location_Id = South },
            new MenuLocations { Menu_Id = Lunch, Location_Id = South });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    // One order item on the given menu, from a table at the given location
    private void SeedOrderAt(int locationId, OrderStatus status = OrderStatus.Pending, int menuId = Dinner)
    {
        var sessionId = 100 + locationId;
        _context.DiningSessions.Add(new DiningSession { Session_Id = sessionId, Menu_Id = menuId, Location_Id = locationId, Started_At = DateTime.UtcNow });
        _context.SessionOrders.Add(new SessionOrder
        {
            session_id = sessionId,
            Bill_Id = sessionId,
            Status = status,
            Created_At = DateTime.UtcNow,
            OrderItems = [new OrderItems { Menu_Id = menuId, Item_Id = 1, Quantity = 1, Order_Item_Status = status }],
        });
        _context.SaveChanges();
    }

    private bool IsAssigned(int menuId, int locationId) =>
        _context.MenuLocations.Any(ml => ml.Menu_Id == menuId && ml.Location_Id == locationId);

    [Theory]
    [InlineData(0, North)]
    [InlineData(-1, North)]
    [InlineData(Dinner, 0)]
    [InlineData(Dinner, -1)]
    public async Task AddLocationToMenu_NonPositiveIds_ReturnsBadRequest(int menuId, int locationId)
    {
        var before = _context.MenuLocations.Count();

        var result = await _controller.AddLocationToMenu(menuId, locationId);

        result.Should().BeOfType<BadRequestObjectResult>();
        _context.MenuLocations.Count().Should().Be(before);
    }

    [Fact]
    public async Task AddLocationToMenu_MenuDoesNotExist_ReturnsNotFound()
    {
        var result = await _controller.AddLocationToMenu(Missing, East);

        result.Should().BeOfType<NotFoundObjectResult>();
        IsAssigned(Missing, East).Should().BeFalse();
    }

    [Fact]
    public async Task AddLocationToMenu_LocationDoesNotExist_ReturnsNotFound()
    {
        var result = await _controller.AddLocationToMenu(Dinner, Missing);

        result.Should().BeOfType<NotFoundObjectResult>();
        IsAssigned(Dinner, Missing).Should().BeFalse();
    }

    [Fact]
    public async Task AddLocationToMenu_NewMenuPairing_SavesIt()
    {
        var result = await _controller.AddLocationToMenu(Dinner, East);

        result.Should().BeOfType<OkResult>();
        IsAssigned(Dinner, East).Should().BeTrue();
    }

    [Fact]
    public async Task AddLocationToMenu_AlreadyAssigned_DoesNotCreateDuplicate()
    {
        // PUT twice must not create a duplicate row
        var result = await _controller.AddLocationToMenu(Dinner, North);

        result.Should().BeOfType<OkResult>();
        _context.MenuLocations.Count(ml => ml.Menu_Id == Dinner && ml.Location_Id == North).Should().Be(1);
    }

    [Fact]
    public async Task AddLocationToMenu_DoesNotTouchOthers()
    {
        var before = _context.MenuLocations.Count();

        await _controller.AddLocationToMenu(Lunch, North);

        _context.MenuLocations.Count().Should().Be(before + 1);
        IsAssigned(Dinner, North).Should().BeTrue();
        IsAssigned(Dinner, South).Should().BeTrue();
        IsAssigned(Lunch, South).Should().BeTrue();
    }

    [Theory]
    [InlineData(0, South)]
    [InlineData(-1, South)]
    [InlineData(Dinner, 0)]
    [InlineData(Dinner, -1)]
    public async Task DeleteLocationToMenu_NonPositiveIds_ReturnsBadRequest(int menuId, int locationId)
    {
        var before = _context.MenuLocations.Count();

        var result = await _controller.DeleteLocationToMenu(menuId, locationId);

        result.Should().BeOfType<BadRequestObjectResult>();
        _context.MenuLocations.Count().Should().Be(before);
    }

    [Fact]
    public async Task DeleteLocationToMenu_NoOrders_RemovesOnlyThatPairing()
    {
        var result = await _controller.DeleteLocationToMenu(Dinner, South);

        result.Should().BeOfType<OkObjectResult>();
        IsAssigned(Dinner, South).Should().BeFalse();
        IsAssigned(Dinner, North).Should().BeTrue();
        IsAssigned(Lunch, South).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteLocationToMenu_OpenOrdersAtAnotherLocation_IsAllowed()
    {
        // Arrange: North has an open dinner order; South has none
        SeedOrderAt(North);

        // Act
        var result = await _controller.DeleteLocationToMenu(Dinner, South);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _context.MenuLocations.Should().ContainSingle(ml => ml.Menu_Id == Dinner).Which.Location_Id.Should().Be(North);
    }

    [Fact]
    public async Task DeleteLocationToMenu_OpenOrdersOnAnotherMenu_IsAllowed()
    {
        // Open lunch order at South must not stop Dinner being removed from South
        SeedOrderAt(South, OrderStatus.Pending, Lunch);

        var result = await _controller.DeleteLocationToMenu(Dinner, South);

        result.Should().BeOfType<OkObjectResult>();
        IsAssigned(Dinner, South).Should().BeFalse();
        IsAssigned(Lunch, South).Should().BeTrue();
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Processing)]
    public async Task DeleteLocationToMenu_OpenOrdersAtThatLocation_ReturnsBadRequest(OrderStatus status)
    {
        // Arrange
        SeedOrderAt(South, status);

        // Act
        var result = await _controller.DeleteLocationToMenu(Dinner, South);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        IsAssigned(Dinner, South).Should().BeTrue();
    }

    [Theory]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task DeleteLocationToMenu_FinishedOrdersAtThatLocation_IsAllowed(OrderStatus status)
    {
        SeedOrderAt(South, status);

        var result = await _controller.DeleteLocationToMenu(Dinner, South);

        result.Should().BeOfType<OkObjectResult>();
        IsAssigned(Dinner, South).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteLocationToMenu_AlreadyRemoved_ReturnsNotFound()
    {
        // Act: e.g. the location itself was deleted, which removes its menus
        _context.MenuLocations.Remove(_context.MenuLocations.Single(ml => ml.Menu_Id == Dinner && ml.Location_Id == South));
        _context.SaveChanges();
        var result = await _controller.DeleteLocationToMenu(Dinner, South);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteLocationToMenu_NeverAssigned_ReturnsNotFound()
    {
        var result = await _controller.DeleteLocationToMenu(Dinner, East);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}