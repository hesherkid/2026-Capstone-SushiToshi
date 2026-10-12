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
using back_end.DTO.Analytics;
using back_end.Services.Analytics;

namespace back_end.Tests.Controllers;

public class OrderTimingControllerTests : IDisposable
{
    private const string WeekFrom = "2025-01-06";
    private const string WeekTo = "2025-01-12";

    // Reference data ids
    private const int North = 1, South = 2;
    private const int DinnerMenu = 1;
    private const int Nigiri = 1, HotDishes = 2;
    private const int Spicy = 1, Vegan = 2;

    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<OrderTimingController>> _mockLogger;
    private readonly OrderTimingController _controller;
    private int _nextSessionId = 1;
    private int _nextOrderId = 1;
    private int _nextLineId = 1;

    public OrderTimingControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _mockLogger = new Mock<ILogger<OrderTimingController>>();

        _controller = new OrderTimingController(
            new OrderTimingService(_context, TimeProvider.System),
            _mockLogger.Object);

        SeedTestData();
    }

    private static DateTime Mst(int day, int hour, int minute = 0) =>
        new DateTime(2025, 1, day, hour, minute, 0, DateTimeKind.Utc).AddHours(7);

    private void SeedTestData()
    {
        _context.Locations.AddRange(
            new Locations { Location_Id = North, Name = "North", Address_Primary = "1 North St", City = "Edmonton", Province = "AB", Postal_Code = "T5K 2B7", Phone_Number = "780-000-0001" },
            new Locations { Location_Id = South, Name = "South", Address_Primary = "1 South St", City = "Edmonton", Province = "AB", Postal_Code = "T6H 1A1", Phone_Number = "780-000-0002" });

        _context.Menus.Add(
            new Menu { Menu_id = DinnerMenu, Name = "Dinner", Description = "Dinner", Start_time = new TimeOnly(16, 0), End_time = new TimeOnly(22, 0), Is_active = true });

        _context.MenuLocations.AddRange(
            new MenuLocations { Menu_Id = DinnerMenu, Location_Id = North },
            new MenuLocations { Menu_Id = DinnerMenu, Location_Id = South });

        _context.Categories.AddRange(
            new Category { Category_id = Nigiri, Category_name = "Nigiri", Description = "Nigiri" },
            new Category { Category_id = HotDishes, Category_name = "Hot Dishes", Description = "Hot Dishes" });

        _context.Tags.AddRange(
            new Tag { tag_id = Spicy, tag_name = "Spicy", tag_color = "#FF0000" },
            new Tag { tag_id = Vegan, tag_name = "Vegan", tag_color = "#00FF00" });

        _context.SaveChanges();
    }

    private int AddSession(DateTime startedAt, DateTime? endedAt = null, int location = North)
    {
        var session = new DiningSession
        {
            Session_Id = _nextSessionId++,
            Location_Id = location,
            Menu_Id = DinnerMenu,
            Started_At = startedAt,
            Ended_At = endedAt
        };

        _context.DiningSessions.Add(session);
        _context.SaveChanges();
        return session.Session_Id;
    }

    private int AddOrder(
        int sessionId,
        DateTime createdAt,
        DateTime? approvedAt = null,
        DateTime? completedAt = null,
        OrderStatus status = OrderStatus.Delivered,
        int billId = 1)
    {
        var order = new SessionOrder
        {
            Order_Id = _nextOrderId++,
            session_id = sessionId,
            Bill_Id = billId,
            Status = status,
            Created_At = createdAt,
            Approved_At = approvedAt,
            Completed_At = completedAt
        };

        _context.SessionOrders.Add(order);
        _context.SaveChanges();
        return order.Order_Id;
    }

    private void AddItem(int id, string name, int categoryId = Nigiri, int[]? tags = null)
    {
        _context.MenuItems.Add(new Menu_Item
        {
            item_id = id,
            Name = name,
            Description = name,
            Category_id = categoryId,
            Status = MenuItemStatus.Available
        });

        _context.MenuItemAssignments.Add(new MenuItemAssignment
        {
            Menu_Id = DinnerMenu,
            Item_Id = id,
            Price = 5m,
            Status = MenuItemStatus.Available,
            Is_Add_On = false
        });

        foreach (var tagId in tags ?? Array.Empty<int>())
        {
            _context.MenuItemTags.Add(new MenuItemTag { Menu_item_id = id, Tag_id = tagId });
        }

        _context.SaveChanges();
    }

    private void AddLine(int orderId, int itemId, DateTime? completedAt, OrderStatus status = OrderStatus.Delivered)
    {
        _context.OrderItems.Add(new OrderItems
        {
            Order_Item_Id = _nextLineId++,
            Order_Key = orderId,
            Menu_Id = DinnerMenu,
            Item_Id = itemId,
            Quantity = 1,
            Price_At_Time = 0m,
            Order_Item_Status = status,
            Completed_At = completedAt
        });

        _context.SaveChanges();
    }

    private void AddDeliveries(int itemId, int minutes, int count = OrderTimingService.MinDeliveries, int location = North)
    {
        var session = AddSession(Mst(7, 17), location: location);
        var order = AddOrder(session, Mst(7, 18), Mst(7, 18, 5), Mst(7, 18, 5 + minutes));
        for (var i = 0; i < count; i++)
        {
            AddLine(order, itemId, Mst(7, 18, 5 + minutes));
        }
    }

    private async Task<OrderTimingResponseDTO> GetWeekAsync(
        int? locationId = North, int? categoryId = null, int? tagId = null, int top = 5)
    {
        var result = await _controller.GetOrderTiming("custom", WeekFrom, WeekTo, locationId, categoryId, tagId, top);

        result.Should().BeOfType<OkObjectResult>();
        var value = (result as OkObjectResult)!.Value;
        value.Should().BeOfType<OrderTimingResponseDTO>();
        return (OrderTimingResponseDTO)value!;
    }

    [Fact]
    public async Task GetOrderTiming_SessionDuration_ReturnsAverageMedianAndOverTimeLimit()
    {
        // Arrange
        AddSession(Mst(7, 17), Mst(7, 18));
        AddSession(Mst(7, 17), Mst(7, 18, 20));
        AddSession(Mst(7, 17), Mst(7, 18, 40));

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.SessionDuration.Count.Should().Be(3);
        response.SessionDuration.AverageMinutes.Should().Be(80.0m);
        response.SessionDuration.MedianMinutes.Should().Be(80.0m);
        response.Summary.SessionsOverTimeLimit.Should().Be(1);
        response.Summary.TimeLimitMinutes.Should().Be(90);
    }

    [Fact]
    public async Task GetOrderTiming_OpenSessions_CountedButLeftOutOfDuration()
    {
        // Arrange
        AddSession(Mst(7, 17), Mst(7, 18));
        AddSession(Mst(7, 17), endedAt: null);

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.Summary.SessionsStarted.Should().Be(2);
        response.Summary.SessionsOpen.Should().Be(1);
        response.SessionDuration.Count.Should().Be(1);
    }

    [Fact]
    public async Task GetOrderTiming_PeakHour_IsLocalHourWithMostOrders()
    {
        // Arrange
        var session = AddSession(Mst(7, 11));
        AddOrder(session, Mst(7, 12, 10));
        AddOrder(session, Mst(7, 18, 5));
        AddOrder(session, Mst(7, 18, 40));

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.OrdersByHour.Should().HaveCount(24);
        response.OrdersByHour.Single(h => h.Hour == 12).Orders.Should().Be(1);
        response.PeakHour!.Hour.Should().Be(18);
        response.PeakHour.Label.Should().Be("18:00");
        response.PeakHour.Orders.Should().Be(2);
    }

    [Fact]
    public async Task GetOrderTiming_OrdersOnRangeBoundaries_StartIncludedEndExcluded()
    {
        // Arrange
        var session = AddSession(Mst(1, 12));
        AddOrder(session, Mst(5, 23, 50));
        AddOrder(session, Mst(6, 0));
        AddOrder(session, Mst(12, 23, 50));
        AddOrder(session, Mst(13, 0));

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.Summary.OrdersPlaced.Should().Be(2);
    }

    [Fact]
    public async Task GetOrderTiming_Pacing_OneOrderStreamPerTableAcrossBills()
    {
        // Arrange
        var session = AddSession(Mst(7, 17));
        AddOrder(session, Mst(7, 17, 5), billId: 1);
        AddOrder(session, Mst(7, 17, 15), billId: 2);
        AddOrder(session, Mst(7, 17, 35), billId: 1);

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.GapBetweenOrders.Count.Should().Be(2);
        response.GapBetweenOrders.AverageMinutes.Should().Be(15.0m);
    }

    [Fact]
    public async Task GetOrderTiming_CancelledOrders_LeftOut()
    {
        // Arrange
        var session = AddSession(Mst(7, 17));
        AddOrder(session, Mst(7, 17, 5), completedAt: Mst(7, 17, 6), status: OrderStatus.Cancelled);

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.Summary.OrdersPlaced.Should().Be(0);
        response.PeakHour.Should().BeNull();
        response.TimeToFirstOrder.Count.Should().Be(0);
        response.AverageOrdersPerSession.Should().BeNull();
    }

    [Fact]
    public async Task GetOrderTiming_StatusTimings_ReturnsAverageAndMedian()
    {
        // Arrange
        var session = AddSession(Mst(7, 17));
        foreach (var wait in new[] { 2, 4, 6, 8 })
        {
            AddOrder(session, Mst(7, 18), Mst(7, 18, wait), Mst(7, 18, wait + 10));
        }

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.PendingToProcessing.Count.Should().Be(4);
        response.PendingToProcessing.AverageMinutes.Should().Be(5.0m);
        response.PendingToProcessing.MedianMinutes.Should().Be(5.0m);
        response.ProcessingToCompleted.MedianMinutes.Should().Be(10.0m);
    }

    [Fact]
    public async Task GetOrderTiming_PendingAndProcessingOrders_OnlyTimedAsFarAsTheyGot()
    {
        // Arrange
        var session = AddSession(Mst(7, 17));
        AddOrder(session, Mst(7, 18), status: OrderStatus.Pending);
        AddOrder(session, Mst(7, 18), Mst(7, 18, 5), status: OrderStatus.Processing);

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.Summary.OrdersPlaced.Should().Be(2);
        response.PendingToProcessing.Count.Should().Be(1);
        response.ProcessingToCompleted.Count.Should().Be(0);
    }

    [Fact]
    public async Task GetOrderTiming_ItemSpeed_MeasuredFromApprovalAndRankedSlowestFirst()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddItem(11, "Tempura", HotDishes);
        AddDeliveries(10, minutes: 4);
        AddDeliveries(11, minutes: 20);

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.SlowestItems.Select(i => i.Name).Should().Equal("Tempura", "Salmon");
        response.SlowestItems[0].Rank.Should().Be(1);
        response.SlowestItems[0].MedianMinutes.Should().Be(20.0m);
        response.SlowestItems[0].CategoryName.Should().Be("Hot Dishes");
        response.SlowestItems[1].MedianMinutes.Should().Be(4.0m);
        response.SlowestItems[1].Deliveries.Should().Be(5);
    }

    [Fact]
    public async Task GetOrderTiming_FewerThanMinDeliveries_NotRanked()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddItem(11, "Tempura", HotDishes);
        AddDeliveries(10, minutes: 4);
        AddDeliveries(11, minutes: 30, count: 4);

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.SlowestItems.Select(i => i.Name).Should().Equal("Salmon");
        response.Summary.ItemsRanked.Should().Be(1);
        response.Summary.MinDeliveries.Should().Be(5);
    }

    [Fact]
    public async Task GetOrderTiming_ManyItems_SlowestAndFastestDoNotOverlap()
    {
        // Arrange
        for (var i = 1; i <= 12; i++)
        {
            AddItem(100 + i, $"Item {i:00}");
            AddDeliveries(100 + i, minutes: i);
        }

        // Act
        var response = await GetWeekAsync(top: 5);

        // Assert
        response.SlowestItems.Select(i => i.Name).Should().Equal("Item 12", "Item 11", "Item 10", "Item 09", "Item 08");
        response.FastestItems.Select(i => i.Name).Should().Equal("Item 01", "Item 02", "Item 03", "Item 04", "Item 05");
    }

    [Fact]
    public async Task GetOrderTiming_CancelledAndUndeliveredLines_AreNotTimed()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddDeliveries(10, minutes: 4);
        var session = AddSession(Mst(7, 17));
        var order = AddOrder(session, Mst(7, 18), Mst(7, 18, 5), status: OrderStatus.Processing);
        AddLine(order, 10, Mst(7, 18, 40), OrderStatus.Cancelled);
        AddLine(order, 10, null, OrderStatus.Processing);

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.SlowestItems.Single().Deliveries.Should().Be(5);
        response.SlowestItems.Single().MedianMinutes.Should().Be(4.0m);
    }

    [Fact]
    public async Task GetOrderTiming_CategoryFilter_RanksWithinCategoryAndKeepsAllOptions()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddItem(11, "Tempura", HotDishes);
        AddDeliveries(10, minutes: 4);
        AddDeliveries(11, minutes: 20);

        // Act
        var response = await GetWeekAsync(categoryId: Nigiri);

        // Assert
        response.SlowestItems.Select(i => i.Name).Should().Equal("Salmon");
        response.SlowestItems[0].Rank.Should().Be(1);
        response.Filters.Categories.Select(c => c.Name).Should().BeEquivalentTo("Nigiri", "Hot Dishes");
    }

    [Fact]
    public async Task GetOrderTiming_TagFilter_ReturnsOnlyTaggedItemsAndKeepsAllOptions()
    {
        // Arrange
        AddItem(10, "Spicy Tuna", tags: new[] { Spicy });
        AddItem(11, "Cucumber Roll", tags: new[] { Vegan });
        AddDeliveries(10, minutes: 6);
        AddDeliveries(11, minutes: 3);

        // Act
        var response = await GetWeekAsync(tagId: Spicy);

        // Assert
        response.SlowestItems.Select(i => i.Name).Should().Equal("Spicy Tuna");
        response.SlowestItems[0].Tags.Select(t => t.Name).Should().Equal("Spicy");
        response.Filters.Tags.Select(t => t.Name).Should().BeEquivalentTo("Spicy", "Vegan");
    }

    [Fact]
    public async Task GetOrderTiming_ItemFilters_DoNotChangeSessionOrOrderFigures()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddItem(11, "Tempura", HotDishes);
        AddDeliveries(10, minutes: 4);
        AddDeliveries(11, minutes: 20);

        // Act
        var all = await GetWeekAsync();
        var nigiriOnly = await GetWeekAsync(categoryId: Nigiri);

        // Assert
        nigiriOnly.Summary.OrdersPlaced.Should().Be(all.Summary.OrdersPlaced);
        nigiriOnly.Summary.SessionsStarted.Should().Be(all.Summary.SessionsStarted);
        nigiriOnly.PendingToProcessing.Count.Should().Be(all.PendingToProcessing.Count);
    }

    [Fact]
    public async Task GetOrderTiming_WithLocation_ExcludesOtherLocations()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddDeliveries(10, minutes: 4, location: North);
        AddDeliveries(10, minutes: 4, location: South);

        // Act
        var northOnly = await GetWeekAsync(locationId: North);
        var all = await GetWeekAsync(locationId: null);

        // Assert
        northOnly.Summary.SessionsStarted.Should().Be(1);
        northOnly.Summary.OrdersPlaced.Should().Be(1);
        northOnly.SlowestItems.Single().Deliveries.Should().Be(5);
        northOnly.Location!.Name.Should().Be("North");

        all.Summary.SessionsStarted.Should().Be(2);
        all.Summary.OrdersPlaced.Should().Be(2);
        all.SlowestItems.Single().Deliveries.Should().Be(10);
        all.Location.Should().BeNull();
    }

    [Fact]
    public async Task GetOrderTiming_UnknownLocation_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetOrderTiming("custom", WeekFrom, WeekTo, 999, null, null);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(26)]
    public async Task GetOrderTiming_TopOutOfRange_ReturnsValidationProblem(int top)
    {
        // Act
        var result = await _controller.GetOrderTiming("custom", WeekFrom, WeekTo, North, null, null, top);

        // Assert
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        var problem = objectResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Errors.Should().ContainKey("top");
    }

    [Theory]
    [InlineData("weekly", null, null, "range")]
    [InlineData("custom", null, WeekTo, "from")]
    [InlineData("custom", WeekTo, WeekFrom, "from")]
    public async Task GetOrderTiming_InvalidRange_ReturnsValidationProblem(
        string range, string? from, string? to, string expectedKey)
    {
        // Act
        var result = await _controller.GetOrderTiming(range, from, to, North, null, null);

        // Assert
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        var problem = objectResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Errors.Should().ContainKey(expectedKey);
    }

    [Fact]
    public async Task GetOrderTiming_NoData_ReturnsOkWithEmptyResults()
    {
        // Act
        var response = await GetWeekAsync();

        // Assert
        response.Summary.SessionsStarted.Should().Be(0);
        response.Summary.OrdersPlaced.Should().Be(0);
        response.SessionDuration.AverageMinutes.Should().BeNull();
        response.PeakHour.Should().BeNull();
        response.OrdersByHour.Should().HaveCount(24);
        response.PendingToProcessing.Count.Should().Be(0);
        response.SlowestItems.Should().BeEmpty();
        response.FastestItems.Should().BeEmpty();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}