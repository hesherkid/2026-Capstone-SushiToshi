using System.Security.Claims;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Helpers;

namespace back_end.Tests.Controllers;

public class MyItemsControllerTests : IDisposable
{
    private const int Customer = 50, OtherCustomer = 51, Guest = 52;
    private const int Salmon = 10, Tuna = 11, Miso = 12, Edamame = 13;

    private readonly ApplicationDbContext _context;
    private readonly MyItemsController _controller;

    public MyItemsControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _controller = new MyItemsController(_context, new Mock<ILogger<MyItemsController>>().Object);

        _context.MenuItems.AddRange(
            new Menu_Item { item_id = Salmon, Name = "Salmon", Description = "Salmon", Category_id = 1 },
            new Menu_Item { item_id = Tuna, Name = "Tuna", Description = "Tuna", Category_id = 1 },
            new Menu_Item { item_id = Miso, Name = "Miso", Description = "Miso", Category_id = 2 },
            new Menu_Item { item_id = Edamame, Name = "Edamame", Description = "Edamame", Category_id = 2 });
        _context.SaveChanges();

        SignIn(Customer);
    }

    public void Dispose() => _context.Dispose();

    private void SignIn(int userId, bool guest = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Customer"),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, guest ? GuestAccounts.NewEmail() : $"user{userId}@example.com"),
        };
        if (guest) claims.Add(new Claim(GuestAccounts.TokenClaim, "true"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
        };
    }

    // One order holding the given items, placed `hoursAgo`
    private void AddOrder(int userId, double hoursAgo, OrderStatus status, params (int itemId, OrderStatus itemStatus)[] items)
    {
        var order = new SessionOrder
        {
            session_id = 1,
            Bill_Id = 1,
            User_Id = userId,
            Status = status,
            Created_At = DateTime.UtcNow.AddHours(-hoursAgo),
        };
        foreach (var (itemId, itemStatus) in items)
        {
            order.OrderItems.Add(new OrderItems { Menu_Id = 1, Item_Id = itemId, Quantity = 1, Order_Item_Status = itemStatus });
        }
        _context.SessionOrders.Add(order);
        _context.SaveChanges();
    }

    private static List<int> IdsOf(IActionResult result) =>
        (List<int>)result.Should().BeOfType<OkObjectResult>().Subject.Value!;

    private static int? StatusOf(IActionResult result) => (result as IStatusCodeActionResult)?.StatusCode;

    [Fact]
    public async Task Favourites_AddedItems_ListNewestFirst()
    {
        // Act
        (await _controller.AddFavourite(Salmon)).Should().BeOfType<NoContentResult>();
        await Task.Delay(5);
        await _controller.AddFavourite(Miso);

        // Assert
        IdsOf(await _controller.GetFavourites()).Should().Equal(Miso, Salmon);
    }

    [Fact]
    public async Task AddFavourite_Twice_KeepsOneRow()
    {
        // Act
        await _controller.AddFavourite(Salmon);
        var result = await _controller.AddFavourite(Salmon);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _context.FavouriteItems.Should().ContainSingle();
    }

    [Fact]
    public async Task AddFavourite_UnknownItem_ReturnsNotFound()
    {
        // Act
        var result = await _controller.AddFavourite(999);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        _context.FavouriteItems.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveFavourite_RemovesOnlyThatItem_AndIgnoresOnesThatArentFavourites()
    {
        // Arrange
        await _controller.AddFavourite(Salmon);
        await _controller.AddFavourite(Tuna);

        // Act
        (await _controller.RemoveFavourite(Salmon)).Should().BeOfType<NoContentResult>();
        (await _controller.RemoveFavourite(Edamame)).Should().BeOfType<NoContentResult>();

        // Assert
        IdsOf(await _controller.GetFavourites()).Should().Equal(Tuna);
    }

    [Fact]
    public async Task Favourites_AreEachCustomersOwn()
    {
        // Arrange
        await _controller.AddFavourite(Salmon);

        // Act
        SignIn(OtherCustomer);

        // Assert
        IdsOf(await _controller.GetFavourites()).Should().BeEmpty();
    }

    [Fact]
    public async Task OrderedBefore_ListsDistinctItems_MostRecentlyOrderedFirst()
    {
        // Arrange: Salmon on an old visit and again today, Miso yesterday
        AddOrder(Customer, hoursAgo: 72, OrderStatus.Delivered, (Salmon, OrderStatus.Delivered));
        AddOrder(Customer, hoursAgo: 24, OrderStatus.Delivered, (Miso, OrderStatus.Delivered));
        AddOrder(Customer, hoursAgo: 1, OrderStatus.Pending, (Salmon, OrderStatus.Pending));

        // Act
        var ids = IdsOf(await _controller.GetOrderedBefore());

        // Assert
        ids.Should().Equal(Salmon, Miso);
    }

    [Fact]
    public async Task OrderedBefore_SkipsCancelledOrdersAndItems_AndOtherCustomersOrders()
    {
        // Arrange
        AddOrder(Customer, hoursAgo: 5, OrderStatus.Cancelled, (Tuna, OrderStatus.Pending));
        AddOrder(Customer, hoursAgo: 4, OrderStatus.Delivered, (Edamame, OrderStatus.Cancelled), (Miso, OrderStatus.Delivered));
        AddOrder(OtherCustomer, hoursAgo: 3, OrderStatus.Delivered, (Salmon, OrderStatus.Delivered));

        // Act
        var ids = IdsOf(await _controller.GetOrderedBefore());

        // Assert
        ids.Should().Equal(Miso);
    }

    [Fact]
    public async Task Guests_AreTurnedAway_FromEveryEndpoint()
    {
        // Arrange
        SignIn(Guest, guest: true);

        // Act + Assert
        StatusOf(await _controller.GetFavourites()).Should().Be(StatusCodes.Status403Forbidden);
        StatusOf(await _controller.AddFavourite(Salmon)).Should().Be(StatusCodes.Status403Forbidden);
        StatusOf(await _controller.RemoveFavourite(Salmon)).Should().Be(StatusCodes.Status403Forbidden);
        StatusOf(await _controller.GetOrderedBefore()).Should().Be(StatusCodes.Status403Forbidden);
        _context.FavouriteItems.Should().BeEmpty();
    }
}
