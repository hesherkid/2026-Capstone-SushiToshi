using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.Analytics;
using back_end.Services.Analytics;

namespace back_end.Tests.Controllers;

public class BrowsingBehaviorControllerTests : IDisposable
{
    private const string WeekFrom = "2025-01-06";
    private const string WeekTo = "2025-01-12";

    // Reference data ids
    private const int North = 1, South = 2;
    private const int DinnerMenu = 1;
    private const int Nigiri = 1, Desserts = 2;
    private const int Spicy = 1;
    private const int S1 = 1, S2 = 2, S3 = 3, S4 = 4;

    private readonly ApplicationDbContext _context;
    private readonly BrowsingBehaviorController _controller;
    private readonly Dictionary<int, int> _sessionLocation = new();
    private int _nextOrderItemId = 1;
    private long _nextViewId = 1;

    public BrowsingBehaviorControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _controller = new BrowsingBehaviorController(
            new BrowsingBehaviorService(_context, TimeProvider.System),
            new Mock<ILogger<BrowsingBehaviorController>>().Object);

        SeedTestData();
    }

    public void Dispose() => _context.Dispose();

    private static DateTime Mst(int day, int hour, int minute = 0) =>
        new DateTime(2025, 1, day, hour, minute, 0, DateTimeKind.Utc).AddHours(7);

    private void SeedTestData()
    {
        _context.Locations.AddRange(
            new Locations { Location_Id = North, Name = "North", Address_Primary = "1 North St", City = "Edmonton", Province = "AB", Postal_Code = "T5K 2B7", Phone_Number = "780-000-0001" },
            new Locations { Location_Id = South, Name = "South", Address_Primary = "1 South St", City = "Edmonton", Province = "AB", Postal_Code = "T6H 1A1", Phone_Number = "780-000-0002" });

        _context.Menus.Add(new Menu { Menu_id = DinnerMenu, Name = "Dinner", Description = "Dinner", Start_time = new TimeOnly(16, 0), End_time = new TimeOnly(22, 0), Is_active = true });

        _context.MenuLocations.AddRange(
            new MenuLocations { Menu_Id = DinnerMenu, Location_Id = North },
            new MenuLocations { Menu_Id = DinnerMenu, Location_Id = South });

        _context.Categories.AddRange(
            new Category { Category_id = Nigiri, Category_name = "Nigiri", Description = "Nigiri" },
            new Category { Category_id = Desserts, Category_name = "Desserts", Description = "Desserts" });

        _context.Tags.Add(new Tag { tag_id = Spicy, tag_name = "Spicy", tag_color = "#FF0000" });

        _context.SaveChanges();

        AddSession(S1, North);
        AddSession(S2, North);
        AddSession(S3, North);
        AddSession(S4, South);
    }

    private void AddSession(int id, int locationId)
    {
        _sessionLocation[id] = locationId;
        _context.DiningSessions.Add(new DiningSession { Session_Id = id, Location_Id = locationId, Menu_Id = DinnerMenu, Started_At = Mst(6, 17) });
        _context.Bills.Add(new Billing { Bill_Id = id, Session_Id = id, Bill_Name = $"Bill {id}", Adult_Count = 1, Total_Count = 1, Status = BillStatus.Open, Created_At = Mst(6, 17) });
        _context.SessionOrders.Add(new SessionOrder { Order_Id = id, session_id = id, Bill_Id = id, Status = OrderStatus.Delivered, Created_At = Mst(6, 17) });
        _context.SaveChanges();
    }

    private void AddItem(
        int id,
        string name,
        int categoryId = Nigiri,
        MenuItemStatus status = MenuItemStatus.Available,
        int[]? tags = null)
    {
        _context.MenuItems.Add(new Menu_Item { item_id = id, Name = name, Description = name, Category_id = categoryId, Status = status });
        _context.MenuItemAssignments.Add(new MenuItemAssignment { Menu_Id = DinnerMenu, Item_Id = id, Price = 0m, Status = MenuItemStatus.Available });

        foreach (var tagId in tags ?? Array.Empty<int>())
        {
            _context.MenuItemTags.Add(new MenuItemTag { Menu_item_id = id, Tag_id = tagId });
        }

        _context.SaveChanges();
    }

    private void AddView(int itemId, int sessionId, int seconds, DateTime viewedAtUtc, bool wasAvailable = true)
    {
        _context.MenuItemViews.Add(new MenuItemView
        {
            View_Id = _nextViewId++,
            Item_Id = itemId,
            Session_Id = sessionId,
            Menu_Id = DinnerMenu,
            Location_Id = _sessionLocation[sessionId],
            Viewed_At = viewedAtUtc,
            View_Seconds = seconds,
            Was_Available = wasAvailable
        });
        _context.SaveChanges();
    }

    private void AddViews(int itemId, int sessionId, int count)
    {
        for (var i = 0; i < count; i++) AddView(itemId, sessionId, 10, Mst(7, 18));
    }

    private void AddSale(int itemId, int sessionId, int quantity, DateTime? completedAtUtc, OrderStatus status = OrderStatus.Delivered)
    {
        _context.OrderItems.Add(new OrderItems
        {
            Order_Item_Id = _nextOrderItemId++,
            Order_Key = sessionId,
            Menu_Id = DinnerMenu,
            Item_Id = itemId,
            Quantity = quantity,
            Price_At_Time = 0m,
            Order_Item_Status = status,
            Completed_At = completedAtUtc
        });
        _context.SaveChanges();
    }

    private async Task<BrowsingBehaviorResponseDTO> GetWeekAsync(
        int? locationId = null, int? categoryId = null, int? tagId = null, int top = 5, int minViews = 10)
    {
        var result = await _controller.GetBrowsingBehavior("custom", WeekFrom, WeekTo, locationId, categoryId, tagId, top, minViews);

        result.Should().BeOfType<OkObjectResult>();
        var value = (result as OkObjectResult)!.Value;
        value.Should().BeOfType<BrowsingBehaviorResponseDTO>();
        return (BrowsingBehaviorResponseDTO)value!;
    }

    private static BrowsingItemDTO Item(BrowsingBehaviorResponseDTO response, string name) =>
        response.Items.Single(i => i.Name == name);

    [Fact]
    public async Task GetBrowsingBehavior_ViewsUnderFiveSeconds_AreNotCountedButReported()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddView(10, S1, 4, Mst(7, 18));
        AddView(10, S1, 5, Mst(7, 18));

        // Act
        var response = await GetWeekAsync();

        // Assert
        var salmon = Item(response, "Salmon");
        salmon.Views.Should().Be(1, "5 seconds is the shortest view that counts");
        salmon.ShortViews.Should().Be(1);
        salmon.TotalViewSeconds.Should().Be(5);
        response.Summary.ShortViewsExcluded.Should().Be(1);
        response.Summary.MinViewSeconds.Should().Be(5);
    }

    [Fact]
    public async Task GetBrowsingBehavior_ViewsOnRangeBoundaries_StartIncludedEndExcluded()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddView(10, S1, 10, Mst(5, 23, 59));
        AddView(10, S1, 10, Mst(6, 0));
        AddView(10, S1, 10, Mst(12, 23, 59));
        AddView(10, S1, 10, Mst(13, 0));

        // Act
        var response = await GetWeekAsync();

        // Assert
        Item(response, "Salmon").Views.Should().Be(2);
    }

    [Fact]
    public async Task GetBrowsingBehavior_OrderInSameSession_Converts_OrderInOtherSessionDoesNot()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddView(10, S1, 10, Mst(7, 18));
        AddView(10, S2, 20, Mst(7, 18));
        AddSale(10, S1, 2, Mst(7, 19));
        AddSale(10, S3, 3, Mst(7, 19));

        // Act
        var response = await GetWeekAsync();

        // Assert
        var salmon = Item(response, "Salmon");
        salmon.ViewingSessions.Should().Be(2);
        salmon.OrderingSessions.Should().Be(1);
        salmon.ConversionRate.Should().Be(50.0m);
        salmon.UnitsOrderedAfterView.Should().Be(2, "S3 never viewed the item, so its order doesn't count");
    }

    [Fact]
    public async Task GetBrowsingBehavior_CancelledOrPendingOrders_DoNotConvert()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddView(10, S1, 10, Mst(7, 18));
        AddSale(10, S1, 1, Mst(7, 19), OrderStatus.Cancelled);
        AddSale(10, S1, 1, null, OrderStatus.Pending);

        // Act
        var response = await GetWeekAsync();

        // Assert
        Item(response, "Salmon").OrderingSessions.Should().Be(0);
        response.ViewedNotOrdered.Select(i => i.Name).Should().Equal("Salmon");
    }

    [Fact]
    public async Task GetBrowsingBehavior_OrderFinishedAfterWindow_StillConvertsTheView()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddView(10, S1, 10, Mst(12, 23, 59));
        AddSale(10, S1, 1, Mst(13, 0, 20));

        // Act
        var response = await GetWeekAsync();

        // Assert
        Item(response, "Salmon").OrderingSessions.Should().Be(1);
    }

    [Fact]
    public async Task GetBrowsingBehavior_ShortViewThenOrder_DoesNotConvert()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddView(10, S1, 3, Mst(7, 18));
        AddSale(10, S1, 1, Mst(7, 19));

        // Act
        var response = await GetWeekAsync();

        // Assert
        var salmon = Item(response, "Salmon");
        salmon.Views.Should().Be(0);
        salmon.OrderingSessions.Should().Be(0);
        response.Summary.SessionItemViews.Should().Be(0);
    }

    [Fact]
    public async Task GetBrowsingBehavior_WithLocation_UsesTheSessionsLocation()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddView(10, S1, 10, Mst(7, 18));
        AddView(10, S4, 10, Mst(7, 18));

        // Act
        var north = await GetWeekAsync(locationId: North);
        var all = await GetWeekAsync(locationId: null);

        // Assert
        Item(north, "Salmon").Views.Should().Be(1);
        north.Location!.Name.Should().Be("North");
        Item(all, "Salmon").Views.Should().Be(2);
        all.Location.Should().BeNull();
    }

    [Fact]
    public async Task GetBrowsingBehavior_UnknownLocation_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetBrowsingBehavior("custom", WeekFrom, WeekTo, 999, null, null);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Theory]
    [InlineData(0, 10, "top")]
    [InlineData(26, 10, "top")]
    [InlineData(5, 0, "minViews")]
    [InlineData(5, 1001, "minViews")]
    public async Task GetBrowsingBehavior_TopOrMinViewsOutOfRange_ReturnsValidationProblem(int top, int minViews, string expectedKey)
    {
        // Act
        var result = await _controller.GetBrowsingBehavior("custom", WeekFrom, WeekTo, null, null, null, top, minViews);

        // Assert
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        var problem = objectResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Errors.Should().ContainKey(expectedKey);
    }

    [Fact]
    public async Task GetBrowsingBehavior_MostAndLeastViewed_NeverOverlap()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddItem(11, "Tuna");
        AddItem(12, "Eel");
        AddViews(10, S1, 3);
        AddViews(11, S1, 2);
        AddViews(12, S1, 1);

        // Act
        var response = await GetWeekAsync(top: 2);

        // Assert
        response.MostViewed.Select(i => i.Name).Should().Equal("Salmon", "Tuna");
        response.LeastViewed.Select(i => i.Name).Should().Equal("Eel");
        response.MostViewed.Select(i => i.ViewRank).Should().Equal(1, 2);
    }

    [Fact]
    public async Task GetBrowsingBehavior_TiedViews_ShareARank()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddItem(11, "Tuna");
        AddItem(12, "Eel");
        AddViews(10, S1, 3);
        AddViews(11, S1, 3);
        AddViews(12, S1, 1);

        // Act
        var response = await GetWeekAsync();

        // Assert
        Item(response, "Salmon").ViewRank.Should().Be(1);
        Item(response, "Tuna").ViewRank.Should().Be(1);
        Item(response, "Eel").ViewRank.Should().Be(3);
    }

    [Fact]
    public async Task GetBrowsingBehavior_NeverViewed_ListsOnlyItemsOrderableNow()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddItem(11, "Tuna");
        AddItem(12, "Retired Roll", status: MenuItemStatus.Unavailable);
        AddViews(10, S1, 1);

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.NeverViewed.Select(i => i.Name).Should().Equal("Tuna");
        response.Items.Should().NotContain(i => i.Name == "Retired Roll", "it wasn't viewed and can't be ordered");
        response.Summary.ItemsNeverViewed.Should().Be(1);
    }

    [Fact]
    public async Task GetBrowsingBehavior_TopViewedAndOrdered_RanksByOrderingSessionsThenConversion()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddItem(11, "Tuna");
        AddItem(12, "Eel");
        AddItem(13, "Squid");
        foreach (var s in new[] { S1, S2, S3 }) AddView(10, s, 10, Mst(7, 18));
        foreach (var s in new[] { S1, S2 }) AddView(11, s, 10, Mst(7, 18));
        AddView(12, S1, 10, Mst(7, 18));
        AddViews(13, S1, 4);
        AddSale(10, S1, 1, Mst(7, 19));
        AddSale(10, S2, 1, Mst(7, 19));
        AddSale(11, S1, 1, Mst(7, 19));
        AddSale(11, S2, 1, Mst(7, 19));
        AddSale(12, S1, 1, Mst(7, 19));

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.TopViewedAndOrdered.Select(i => i.Name).Should().Equal("Tuna", "Salmon", "Eel");
        response.TopViewedNotOrdered.Select(i => i.Name).Should().Equal("Squid");
        Item(response, "Salmon").ConversionRate.Should().Be(66.7m);
    }

    [Fact]
    public async Task GetBrowsingBehavior_LowConversionHighViews_OnlyItemsWithAtLeastMinViews()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddItem(11, "Tuna");
        AddItem(12, "Eel");
        AddItem(13, "Squid");
        AddViews(10, S1, 2);
        AddViews(11, S1, 4);
        AddViews(12, S1, 12);
        AddViews(13, S2, 20);
        AddSale(13, S2, 1, Mst(7, 19));

        // Act
        var minTen = await GetWeekAsync(minViews: 10);
        var minFifteen = await GetWeekAsync(minViews: 15);

        // Assert
        minTen.Summary.MinViews.Should().Be(10);
        minTen.LowConversionHighViews.Select(i => i.Name).Should().Equal("Eel", "Squid");
        minFifteen.LowConversionHighViews.Select(i => i.Name).Should().Equal("Squid");
    }

    [Fact]
    public async Task GetBrowsingBehavior_AverageViewTime_SplitsOrderedAndNotOrdered()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddView(10, S1, 30, Mst(7, 18));
        AddView(10, S2, 6, Mst(7, 18));
        AddView(10, S2, 10, Mst(7, 18));
        AddSale(10, S1, 1, Mst(7, 19));

        // Act
        var response = await GetWeekAsync();

        // Assert
        var salmon = Item(response, "Salmon");
        salmon.AverageViewSeconds.Should().Be(15.3m);
        salmon.AverageViewSecondsOrdered.Should().Be(30.0m);
        salmon.AverageViewSecondsNotOrdered.Should().Be(8.0m);
        response.Summary.AverageViewSecondsOrdered.Should().Be(30.0m);
        response.Summary.AverageViewSecondsNotOrdered.Should().Be(8.0m);
        response.Summary.ConversionRate.Should().Be(50.0m);
    }

    [Fact]
    public async Task GetBrowsingBehavior_ViewsWhileUnavailable_AreListed()
    {
        // Arrange
        AddItem(10, "Salmon");
        AddView(10, S1, 10, Mst(7, 18), wasAvailable: false);
        AddView(10, S2, 10, Mst(7, 18), wasAvailable: true);

        // Act
        var response = await GetWeekAsync();

        // Assert
        Item(response, "Salmon").UnavailableViews.Should().Be(1);
        response.UnavailableViews.Select(i => i.Name).Should().Equal("Salmon");
        response.Summary.UnavailableViews.Should().Be(1);
    }

    [Fact]
    public async Task GetBrowsingBehavior_CategoryAndTagFilters_ScopeItemsAndSummary()
    {
        // Arrange
        AddItem(10, "Salmon", Nigiri);
        AddItem(20, "Mochi", Desserts, tags: new[] { Spicy });
        AddViews(10, S1, 3);
        AddViews(20, S2, 1);

        // Act
        var desserts = await GetWeekAsync(categoryId: Desserts);
        var spicy = await GetWeekAsync(tagId: Spicy);

        // Assert
        desserts.Items.Select(i => i.Name).Should().Equal("Mochi");
        desserts.Summary.TotalViews.Should().Be(1);
        desserts.Summary.ViewingSessions.Should().Be(1);
        desserts.Filters.Categories.Select(c => c.Name).Should().Equal("Desserts", "Nigiri");

        spicy.Items.Select(i => i.Name).Should().Equal("Mochi");
    }

    [Fact]
    public async Task GetBrowsingBehavior_NoViews_ReturnsNullRatesNotZero()
    {
        // Arrange
        AddItem(10, "Salmon");

        // Act
        var response = await GetWeekAsync();

        // Assert
        response.Summary.TotalViews.Should().Be(0);
        response.Summary.ConversionRate.Should().BeNull();
        response.Summary.AverageViewSeconds.Should().BeNull();
        Item(response, "Salmon").ConversionRate.Should().BeNull();
    }
}
