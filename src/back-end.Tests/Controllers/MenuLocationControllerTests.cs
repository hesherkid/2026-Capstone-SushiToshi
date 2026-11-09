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

namespace back_end.Tests.Controllers;

// DELETE /api/MenuLocation/menu/{menuId}/location/{locationId}
public class MenuLocationControllerTests : IDisposable
{
    private const int Dinner = 1, North = 1, South = 2;

    private readonly ApplicationDbContext _context;
    private readonly MenuLocationController _controller;

    public MenuLocationControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _controller = new MenuLocationController(_context, Mock.Of<ILogger<MenuLocationController>>(), Mock.Of<IWebHostEnvironment>());

        _context.Menus.Add(new Menu { Menu_id = Dinner, Name = "Dinner", Description = "Dinner", Is_active = true });
        _context.Locations.AddRange(
            new Locations { Location_Id = North, Name = "North", Address_Primary = "1 North St", City = "Edmonton", Province = "AB", Postal_Code = "T5K 2B7", Phone_Number = "780-555-0001" },
            new Locations { Location_Id = South, Name = "South", Address_Primary = "2 South St", City = "Edmonton", Province = "AB", Postal_Code = "T6X 1A1", Phone_Number = "780-555-0002" });
        _context.MenuLocations.AddRange(
            new MenuLocations { Menu_Id = Dinner, Location_Id = North },
            new MenuLocations { Menu_Id = Dinner, Location_Id = South });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    // A pending order on the dinner menu, from a table at the given location
    private void SeedOpenOrderAt(int locationId)
    {
        var sessionId = 100 + locationId;
        _context.DiningSessions.Add(new DiningSession { Session_Id = sessionId, Menu_Id = Dinner, Location_Id = locationId, Started_At = DateTime.UtcNow });
        _context.SessionOrders.Add(new SessionOrder
        {
            session_id = sessionId,
            Bill_Id = sessionId,
            Status = OrderStatus.Pending,
            Created_At = DateTime.UtcNow,
            OrderItems = [new OrderItems { Menu_Id = Dinner, Item_Id = 1, Quantity = 1, Order_Item_Status = OrderStatus.Pending }],
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task DeleteLocationToMenu_OpenOrdersAtAnotherLocation_DontBlockIt()
    {
        // Arrange: North has an open dinner order; South has none
        SeedOpenOrderAt(North);

        // Act
        var result = await _controller.DeleteLocationToMenu(Dinner, South);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _context.MenuLocations.Should().ContainSingle(ml => ml.Menu_Id == Dinner).Which.Location_Id.Should().Be(North);
    }

    [Fact]
    public async Task DeleteLocationToMenu_OpenOrdersAtThatLocation_IsRefused()
    {
        // Arrange
        SeedOpenOrderAt(South);

        // Act
        var result = await _controller.DeleteLocationToMenu(Dinner, South);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        _context.MenuLocations.Should().Contain(ml => ml.Menu_Id == Dinner && ml.Location_Id == South);
    }

    [Fact]
    public async Task DeleteLocationToMenu_AlreadyRemoved_ReturnsNotFound()
    {
        // Act: e.g. the location itself was deleted, which removes its menus
        _context.MenuLocations.Remove(_context.MenuLocations.Single(ml => ml.Location_Id == South));
        _context.SaveChanges();
        var result = await _controller.DeleteLocationToMenu(Dinner, South);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
