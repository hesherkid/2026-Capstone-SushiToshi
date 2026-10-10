using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using back_end.controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.Analytics;

namespace back_end.Tests.Controllers;

/// <summary>
/// Tests for GET api/analytics/item-performance.
/// Most tests use a fixed past week (Mon 2025-01-06 to Sun 2025-01-12, Mountain Standard Time, UTC-7)
/// through range=custom, so results never depend on today's date.
/// </summary>
public class AnalyticsControllerItemPerformanceTests : IDisposable
{
    private const string WeekFrom = "2025-01-06";
    private const string WeekTo = "2025-01-12";

    // Reference data ids
    private const int North = 1, South = 2;
    private const int DinnerMenu = 1;      // active, linked to North and South
    private const int RetiredMenu = 2;     // inactive, linked to North
    private const int SouthOnlyMenu = 3;   // active, linked to South only
    private const int Nigiri = 1, Desserts = 2;
    private const int Spicy = 1, Vegan = 2;

    private readonly ApplicationDbContext _context;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly Mock<ILogger<AnalyticsController>> _mockLogger;
    private readonly AnalyticsController _controller;
    private int _nextOrderItemId = 1;

    public AnalyticsControllerItemPerformanceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _mockConfig = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<AnalyticsController>>();

        _controller = new AnalyticsController(_context, _mockConfig.Object, _mockLogger.Object);

        SeedTestData();
    }

    /// <summary>Mountain Standard Time (UTC-7 in January 2025) to UTC.</summary>
    private static DateTime Mst(int day, int hour, int minute = 0) =>
        new DateTime(2025, 1, day, hour, minute, 0, DateTimeKind.Utc).AddHours(7);

    private void SeedTestData()
    {
        _context.Locations.AddRange(
            new Locations { Location_Id = North, Name = "North", Address_Primary = "1 North St", City = "Edmonton", Province = "AB", Postal_Code = "T5K 2B7", Phone_Number = "780-000-0001" },
            new Locations { Location_Id = South, Name = "South", Address_Primary = "1 South St", City = "Edmonton", Province = "AB", Postal_Code = "T6H 1A1", Phone_Number = "780-000-0002" });

        _context.Menus.AddRange(
            new Menu { Menu_id = DinnerMenu, Name = "Dinner", Description = "Dinner", Start_time = new TimeOnly(16, 0), End_time = new TimeOnly(22, 0), Is_active = true },
            new Menu { Menu_id = RetiredMenu, Name = "Retired", Description = "Old menu", Start_time = new TimeOnly(11, 0), End_time = new TimeOnly(15, 0), Is_active = false },
            new Menu { Menu_id = SouthOnlyMenu, Name = "South Lunch", Description = "South only", Start_time = new TimeOnly(11, 0), End_time = new TimeOnly(15, 0), Is_active = true });

        _context.MenuLocations.AddRange(
            new MenuLocations { Menu_Id = DinnerMenu, Location_Id = North },
            new MenuLocations { Menu_Id = DinnerMenu, Location_Id = South },
            new MenuLocations { Menu_Id = RetiredMenu, Location_Id = North },
            new MenuLocations { Menu_Id = SouthOnlyMenu, Location_Id = South });

        _context.Categories.AddRange(
            new Category { Category_id = Nigiri, Category_name = "Nigiri", Description = "Nigiri" },
            new Category { Category_id = Desserts, Category_name = "Desserts", Description = "Desserts" });

        _context.Tags.AddRange(
            new Tag { tag_id = Spicy, tag_name = "Spicy", tag_color = "#FF0000" },
            new Tag { tag_id = Vegan, tag_name = "Vegan", tag_color = "#00FF00" });

        // One dining session, bill and order per location; sales attach to the order for their location.
        _context.DiningSessions.AddRange(
            new DiningSession { Session_Id = North, Location_Id = North, Menu_Id = DinnerMenu, Started_At = Mst(6, 17) },
            new DiningSession { Session_Id = South, Location_Id = South, Menu_Id = DinnerMenu, Started_At = Mst(6, 17) });

        _context.Bills.AddRange(
            new Billing { Bill_Id = North, Session_Id = North, Bill_Name = "North bill", Adult_Count = 1, Total_Count = 1, Status = BillStatus.Open, Created_At = Mst(6, 17) },
            new Billing { Bill_Id = South, Session_Id = South, Bill_Name = "South bill", Adult_Count = 1, Total_Count = 1, Status = BillStatus.Open, Created_At = Mst(6, 17) });

        _context.SessionOrders.AddRange(
            new SessionOrder { Order_Id = North, session_id = North, Bill_Id = North, Status = OrderStatus.Delivered, Created_At = Mst(6, 17) },
            new SessionOrder { Order_Id = South, session_id = South, Bill_Id = South, Status = OrderStatus.Delivered, Created_At = Mst(6, 17) });

        _context.SaveChanges();
    }

    private void AddItem(
        int id,
        string name,
        int categoryId = Nigiri,
        MenuItemStatus status = MenuItemStatus.Available,
        int[]? menus = null,
        MenuItemStatus assignmentStatus = MenuItemStatus.Available,
        int[]? addOnMenus = null,
        int[]? tags = null)
    {
        _context.MenuItems.Add(new Menu_Item
        {
            item_id = id,
            Name = name,
            Description = name,
            Category_id = categoryId,
            Status = status
        });

        foreach (var menuId in menus ?? new[] { DinnerMenu })
        {
            _context.MenuItemAssignments.Add(new MenuItemAssignment
            {
                Menu_Id = menuId,
                Item_Id = id,
                Price = 5m,
                Status = assignmentStatus,
                Is_Add_On = addOnMenus?.Contains(menuId) ?? false
            });
        }

        foreach (var tagId in tags ?? Array.Empty<int>())
        {
            _context.MenuItemTags.Add(new MenuItemTag { Menu_item_id = id, Tag_id = tagId });
        }

        _context.SaveChanges();
    }

    private void AddSale(
        int itemId,
        int quantity,
        DateTime? completedAtUtc,
        OrderStatus status = OrderStatus.Delivered,
        int location = North,
        int menuId = DinnerMenu)
    {
        _context.OrderItems.Add(new OrderItems
        {
            Order_Item_Id = _nextOrderItemId++,
            Order_Key = location, // order id matches location id in the reference data
            Menu_Id = menuId,
            Item_Id = itemId,
            Quantity = quantity,
            Price_At_Time = 5m,
            Order_Item_Status = status,
            Completed_At = completedAtUtc
        });

        _context.SaveChanges();
    }

    private async Task<ItemPerformanceResponseDTO> GetWeekAsync(
        int? locationId = North, int? categoryId = null, int? tagId = null, int top = 5)
    {
        var result = await _controller.GetItemPerformance("custom", WeekFrom, WeekTo, locationId, categoryId, tagId, top);

        result.Should().BeOfType<OkObjectResult>();
        var value = (result as OkObjectResult)!.Value;
        value.Should().BeOfType<ItemPerformanceResponseDTO>();
        return (ItemPerformanceResponseDTO)value!;
    }

    private static ItemPerformanceDTO Item(ItemPerformanceResponseDTO response, string name) =>
        response.Items.Single(i => i.Name == name);

    [Fact]
    public async Task GetItemPerformance_MixedOrderStatuses_CountsOnlyDelivered()
    {
        // Arrange - cancelled items get Completed_At in the live flow, so status must be checked
        AddItem(10, "Salmon");
        AddSale(10, 3, Mst(7, 12));
        AddSale(10, 5, Mst(7, 13), OrderStatus.Cancelled);
        AddSale(10, 2, null, OrderStatus.Pending);
        AddSale(10, 4, Mst(7, 14), OrderStatus.Processing);

        // Act
        var response = await GetWeekAsync();

        // Assert
        Item(response, "Salmon").UnitsSold.Should().Be(3);
        response.Summary.TotalUnitsSold.Should().Be(3);
    }

    [Fact]
    public async Task GetItemPerformance_SalesOnRangeBoundaries_StartIncludedEndExcluded()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddSale(10, 1000, Mst(5, 23, 59));   // before the week
        AddSale(10, 1, Mst(6, 0));           // exactly the start: included
        AddSale(10, 100, Mst(12, 23, 59));   // last minute of the week: included
        AddSale(10, 10, Mst(13, 0));         // exactly the end: excluded

        // Act
        var response = await GetWeekAsync();

        // Assert
        Item(response, "Salmon").UnitsSold.Should().Be(101);
    }

    [Fact]
    public async Task GetItemPerformance_CustomWeek_EchoesLocalDatesAndDayCount()
    {
        // Act
        var response = await GetWeekAsync();

        // Assert
        response.Range.Preset.Should().Be("custom");
        response.Range.From.Should().Be(WeekFrom);
        response.Range.To.Should().Be(WeekTo);
        response.Range.Days.Should().Be(7);
        response.Range.TimeZone.Should().Be("America/Edmonton");
    }

    [Fact]
    public async Task GetItemPerformance_WithLocation_ExcludesOtherLocationsSales()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddSale(10, 3, Mst(7, 12), location: North);
        AddSale(10, 7, Mst(7, 12), location: South);

        // Act
        var north = await GetWeekAsync(locationId: North);
        var all = await GetWeekAsync(locationId: null);

        // Assert
        Item(north, "Salmon").UnitsSold.Should().Be(3);
        north.Location.Should().NotBeNull();
        north.Location!.Name.Should().Be("North");

        Item(all, "Salmon").UnitsSold.Should().Be(10);
        all.Location.Should().BeNull();
    }

    [Fact]
    public async Task GetItemPerformance_UnknownLocation_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetItemPerformance("custom", WeekFrom, WeekTo, 999, null, null);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetItemPerformance_ItemOnMenuNotLinkedToLocation_OnlyAppearsWhereItIsServed()
    {
        // Arrange - South Special is only on a menu linked to South
        AddItem(19, "South Special", menus: new[] { SouthOnlyMenu });

        // Act
        var north = await GetWeekAsync(locationId: North);
        var south = await GetWeekAsync(locationId: South);
        var all = await GetWeekAsync(locationId: null);

        // Assert
        north.Items.Should().NotContain(i => i.Name == "South Special");
        south.ZeroSales.Should().ContainSingle(i => i.Name == "South Special" && i.CurrentlyAvailable);
        all.ZeroSales.Should().ContainSingle(i => i.Name == "South Special");
    }

    [Fact]
    public async Task GetItemPerformance_ItemUnavailableNowButSoldInRange_IsIncludedAndFlagged()
    {
        // Arrange
        AddItem(15, "Retired Roll", status: MenuItemStatus.Unavailable);
        AddSale(15, 4, Mst(8, 18));

        // Act
        var response = await GetWeekAsync();

        // Assert
        var item = Item(response, "Retired Roll");
        item.UnitsSold.Should().Be(4);
        item.Rank.Should().Be(1);
        item.Status.Should().Be("Unavailable");
        item.CurrentlyAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task GetItemPerformance_AssignmentUnavailableButSoldInRange_IsIncludedAndFlagged()
    {
        // Arrange - item itself is Available, but its menu assignment is Unavailable
        AddItem(18, "Assignment Off", assignmentStatus: MenuItemStatus.Unavailable);
        AddSale(18, 2, Mst(8, 18));

        // Act
        var response = await GetWeekAsync();

        // Assert
        var item = Item(response, "Assignment Off");
        item.UnitsSold.Should().Be(2);
        item.CurrentlyAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task GetItemPerformance_NotOrderableNowAndNoSales_IsExcluded()
    {
        // Arrange
        AddItem(10, "Salmon");                                                     // control: available, no sales
        AddItem(16, "Unavailable Item", status: MenuItemStatus.Unavailable);       // item unavailable
        AddItem(18, "Assignment Off", assignmentStatus: MenuItemStatus.Unavailable); // assignment unavailable
        AddItem(17, "Retired Menu Only", menus: new[] { RetiredMenu });            // only on an inactive menu

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.Items.Select(i => i.Name).Should().BeEquivalentTo(new[] { "Salmon" });
        response.ZeroSales.Select(i => i.Name).Should().BeEquivalentTo(new[] { "Salmon" });
    }

    [Fact]
    public async Task GetItemPerformance_SeasonalItem_IsIncludedAndCurrentlyAvailable()
    {
        // Arrange
        AddItem(14, "Seasonal Roll", status: MenuItemStatus.Seasonal);

        // Act
        var response = await GetWeekAsync();

        // Assert
        var item = response.ZeroSales.Single(i => i.Name == "Seasonal Roll");
        item.Status.Should().Be("Seasonal");
        item.CurrentlyAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task GetItemPerformance_TiedUnits_ShareCompetitionRank()
    {
        // Arrange - 10, 7, 7, 3 units -> ranks 1, 2, 2, 4
        AddItem(1, "Alpha"); AddSale(1, 10, Mst(7, 12));
        AddItem(2, "Charlie"); AddSale(2, 7, Mst(7, 12));
        AddItem(3, "Bravo"); AddSale(3, 7, Mst(7, 12));
        AddItem(4, "Delta"); AddSale(4, 3, Mst(7, 12));

        // Act
        var response = await GetWeekAsync();

        // Assert - ties are ordered by name
        response.Items.Select(i => (i.Name, i.Rank)).Should().Equal(
            ("Alpha", 1), ("Bravo", 2), ("Charlie", 2), ("Delta", 4));
    }

    [Fact]
    public async Task GetItemPerformance_ManySellers_BestAndWorstDoNotOverlap()
    {
        // Arrange - six sellers with 6..1 units
        for (var units = 6; units >= 1; units--)
        {
            AddItem(units, $"Item {units}");
            AddSale(units, units, Mst(7, 12));
        }

        // Act
        var response = await GetWeekAsync(top: 2);

        // Assert
        response.Best.Select(i => i.UnitsSold).Should().Equal(6, 5);
        response.Worst.Select(i => i.UnitsSold).Should().Equal(1, 2); // lowest first
        response.Best.Select(i => i.ItemId).Should().NotIntersectWith(response.Worst.Select(i => i.ItemId));
    }

    [Theory]
    [InlineData(3, 2, 2, 1)] // 3 sellers, top 2: worst gets the 1 left over
    [InlineData(2, 2, 2, 0)] // 2 sellers, top 2: nothing left for worst
    [InlineData(1, 5, 1, 0)]
    public async Task GetItemPerformance_FewSellers_WorstOnlyUsesItemsNotInBest(
        int sellers, int top, int expectedBest, int expectedWorst)
    {
        // Arrange
        for (var i = 1; i <= sellers; i++)
        {
            AddItem(i, $"Item {i}");
            AddSale(i, i, Mst(7, 12));
        }

        // Act
        var response = await GetWeekAsync(top: top);

        // Assert
        response.Best.Should().HaveCount(expectedBest);
        response.Worst.Should().HaveCount(expectedWorst);
    }

    [Fact]
    public async Task GetItemPerformance_ZeroSalesItems_ListedSeparatelyWithoutRank()
    {
        // Arrange
        AddItem(1, "Salmon"); AddSale(1, 5, Mst(7, 12));
        AddItem(2, "Zucchini Roll");
        AddItem(3, "Avocado Roll");

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.ZeroSales.Select(i => i.Name).Should().Equal("Avocado Roll", "Zucchini Roll");
        response.ZeroSales.Should().OnlyContain(i => i.Rank == null && i.UnitsSold == 0);
        response.Best.Should().NotContain(i => i.UnitsSold == 0);
        response.Worst.Should().NotContain(i => i.UnitsSold == 0);
        response.Items.Select(i => i.Name).Should().Equal("Salmon", "Avocado Roll", "Zucchini Roll");

        response.Summary.ItemsConsidered.Should().Be(3);
        response.Summary.ItemsWithSales.Should().Be(1);
        response.Summary.ItemsWithZeroSales.Should().Be(2);
    }

    [Fact]
    public async Task GetItemPerformance_ShareOfUnits_RoundedToOneDecimal()
    {
        // Arrange
        AddItem(1, "Salmon"); AddSale(1, 2, Mst(7, 12));
        AddItem(2, "Tuna"); AddSale(2, 1, Mst(7, 12));

        // Act
        var response = await GetWeekAsync();

        // Assert
        Item(response, "Salmon").ShareOfUnits.Should().Be(66.7m);
        Item(response, "Tuna").ShareOfUnits.Should().Be(33.3m);
    }

    [Fact]
    public async Task GetItemPerformance_NoItems_ReturnsOkWithEmptyLists()
    {
        // Act
        var response = await GetWeekAsync();

        // Assert
        response.Items.Should().BeEmpty();
        response.Best.Should().BeEmpty();
        response.Worst.Should().BeEmpty();
        response.ZeroSales.Should().BeEmpty();
        response.AddOns.Should().BeEmpty();
        response.Summary.TotalUnitsSold.Should().Be(0);
    }

    [Fact]
    public async Task GetItemPerformance_CategoryFilter_RanksWithinCategoryAndKeepsAllOptions()
    {
        // Arrange
        AddItem(1, "Salmon", categoryId: Nigiri); AddSale(1, 10, Mst(7, 12));
        AddItem(2, "Mochi", categoryId: Desserts); AddSale(2, 2, Mst(7, 12));

        // Act
        var response = await GetWeekAsync(categoryId: Desserts);

        // Assert
        response.Items.Should().ContainSingle(i => i.Name == "Mochi" && i.Rank == 1);
        response.Items.Single().ShareOfUnits.Should().Be(100m, "share is calculated within the filtered set");
        response.Filters.Categories.Select(c => c.Name).Should().Equal("Desserts", "Nigiri");
    }

    [Fact]
    public async Task GetItemPerformance_TagFilter_ReturnsOnlyTaggedItemsAndKeepsAllOptions()
    {
        // Arrange
        AddItem(1, "Spicy Salmon", tags: new[] { Spicy }); AddSale(1, 3, Mst(7, 12));
        AddItem(2, "Tuna"); AddSale(2, 9, Mst(7, 12));
        AddItem(3, "Mochi", categoryId: Desserts, tags: new[] { Vegan });

        // Act
        var response = await GetWeekAsync(tagId: Spicy);

        // Assert
        response.Items.Select(i => i.Name).Should().Equal("Spicy Salmon");
        response.Items.Single().Tags.Should().ContainSingle(t => t.Name == "Spicy" && t.Color == "#FF0000");
        response.Filters.Tags.Select(t => t.Name).Should().Equal("Spicy", "Vegan");
    }

    [Fact]
    public async Task GetItemPerformance_AddOnSales_CountedPerMenuAndByLocalDay()
    {
        // Arrange - Mochi is an add-on on Dinner but included (not an add-on) on South Lunch
        AddItem(13, "Mochi", categoryId: Desserts,
            menus: new[] { DinnerMenu, SouthOnlyMenu }, addOnMenus: new[] { DinnerMenu });

        AddSale(13, 1, Mst(7, 23, 30));  // Jan 7 local (Jan 8 06:30 UTC)
        AddSale(13, 2, Mst(8, 0, 30));   // Jan 8 local (Jan 8 07:30 UTC, same UTC date)
        AddSale(13, 1, Mst(8, 12));      // Jan 8 again
        AddSale(13, 4, Mst(9, 12), location: South, menuId: SouthOnlyMenu); // not an add-on there

        // Act
        var response = await GetWeekAsync(locationId: null);

        // Assert
        var mochi = Item(response, "Mochi");
        mochi.UnitsSold.Should().Be(8);
        mochi.AddOnUnits.Should().Be(4);
        mochi.AddOnShare.Should().Be(50.0m);
        mochi.AddOnDays.Should().Be(2, "Jan 7 and Jan 8 Mountain Time, even though both are Jan 8 in UTC");

        response.AddOns.Should().ContainSingle(i => i.Name == "Mochi");
        response.Summary.TotalAddOnUnits.Should().Be(4);
    }

    [Fact]
    public async Task GetItemPerformance_AddOns_OrderedByDaysThenUnits()
    {
        // Arrange
        AddItem(1, "Steady Add-on", addOnMenus: new[] { DinnerMenu });
        AddSale(1, 1, Mst(6, 12)); AddSale(1, 1, Mst(7, 12)); AddSale(1, 1, Mst(8, 12)); // 3 days, 3 units

        AddItem(2, "Spike Add-on", addOnMenus: new[] { DinnerMenu });
        AddSale(2, 10, Mst(9, 12));                                                       // 1 day, 10 units

        AddItem(3, "Regular Item");
        AddSale(3, 50, Mst(9, 12));

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.AddOns.Select(i => i.Name).Should().Equal("Steady Add-on", "Spike Add-on");
        Item(response, "Regular Item").AddOnUnits.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(26)]
    public async Task GetItemPerformance_TopOutOfRange_ReturnsValidationProblem(int top)
    {
        // Act
        var result = await _controller.GetItemPerformance("custom", WeekFrom, WeekTo, North, null, null, top);

        // Assert
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        var problem = objectResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Errors.Should().ContainKey("top");
    }

    [Theory]
    [InlineData("weekly", null, null, "range")]
    [InlineData("custom", null, WeekTo, "from")]
    [InlineData("custom", WeekFrom, "2025-13-01", "to")]
    [InlineData("custom", WeekTo, WeekFrom, "from")]
    public async Task GetItemPerformance_InvalidRange_ReturnsValidationProblem(
        string range, string? from, string? to, string expectedKey)
    {
        // Act
        var result = await _controller.GetItemPerformance(range, from, to, North, null, null);

        // Assert
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        var problem = objectResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Errors.Should().ContainKey(expectedKey);
    }

    [Fact]
    public async Task GetItemPerformance_NoRange_DefaultsToLast7FullDays()
    {
        // Act
        var result = await _controller.GetItemPerformance(null, null, null, North, null, null);

        // Assert
        var response = (result as OkObjectResult)!.Value as ItemPerformanceResponseDTO;
        response!.Range.Preset.Should().Be("last7");
        response.Range.Days.Should().Be(7);
        response.Range.TimeZone.Should().Be("America/Edmonton");
    }

    [Fact]
    public async Task GetItemPerformance_ConfiguredTimeZone_IsUsed()
    {
        // Arrange
        _mockConfig.Setup(c => c["Analytics:TimeZoneId"]).Returns("America/Regina");

        // Act
        var result = await _controller.GetItemPerformance("custom", WeekFrom, WeekTo, North, null, null);

        // Assert
        var response = (result as OkObjectResult)!.Value as ItemPerformanceResponseDTO;
        response!.Range.TimeZone.Should().Be("America/Regina");
    }

    [Fact]
    public async Task GetItemPerformance_UnknownTimeZoneConfigured_Returns500AndLogsError()
    {
        // Arrange
        _mockConfig.Setup(c => c["Analytics:TimeZoneId"]).Returns("Not/A_Zone");

        // Act
        var result = await _controller.GetItemPerformance("custom", WeekFrom, WeekTo, North, null, null);

        // Assert
        var error = result.Should().BeOfType<ObjectResult>().Subject;
        error.StatusCode.Should().Be(500);

        _mockLogger.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

}