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
using back_end.DTO.MenuItemViewDTOs;
using back_end.Helpers;

namespace back_end.Tests.Controllers;

/// <summary>Tests for POST api/menu-item-views.</summary>
public class MenuItemViewControllerTests : IDisposable
{
    // Reference data ids
    private const int North = 1;
    private const int DinnerMenu = 1;
    private const int LunchMenu = 2;
    private const int OpenSession = 1, EndedSession = 2;
    private const int GuestUser = 50, OtherCustomer = 51, StaffUser = 52;

    private const int Salmon = 10;
    private const int LunchSpecial = 11;
    private const int SoldOut = 13;
    private const int Retired = 14;

    private readonly ApplicationDbContext _context;
    private readonly MenuItemViewController _controller;

    public MenuItemViewControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _controller = new MenuItemViewController(_context, new Mock<ILogger<MenuItemViewController>>().Object);

        Seed();
        SignIn(GuestUser);
    }

    public void Dispose() => _context.Dispose();

    private void Seed()
    {
        var now = DateTime.UtcNow;

        _context.Menus.AddRange(
            new Menu { Menu_id = DinnerMenu, Name = "Dinner", Description = "Dinner", Is_active = true },
            new Menu { Menu_id = LunchMenu, Name = "Lunch", Description = "Lunch", Is_active = true });

        _context.MenuLocations.AddRange(
            new MenuLocations { Menu_Id = DinnerMenu, Location_Id = North },
            new MenuLocations { Menu_Id = LunchMenu, Location_Id = North });

        _context.MenuItems.AddRange(
            new Menu_Item { item_id = Salmon, Name = "Salmon", Description = "Salmon", Category_id = 1 },
            new Menu_Item { item_id = LunchSpecial, Name = "Lunch Special", Description = "Lunch", Category_id = 1 },
            new Menu_Item { item_id = SoldOut, Name = "Sold Out", Description = "Sold out", Category_id = 1 },
            new Menu_Item { item_id = Retired, Name = "Retired", Description = "Retired", Category_id = 1, Status = MenuItemStatus.Unavailable });

        _context.MenuItemAssignments.AddRange(
            new MenuItemAssignment { Menu_Id = DinnerMenu, Item_Id = Salmon, Status = MenuItemStatus.Available },
            new MenuItemAssignment { Menu_Id = LunchMenu, Item_Id = LunchSpecial, Status = MenuItemStatus.Available },
            new MenuItemAssignment { Menu_Id = DinnerMenu, Item_Id = SoldOut, Status = MenuItemStatus.Unavailable },
            new MenuItemAssignment { Menu_Id = DinnerMenu, Item_Id = Retired, Status = MenuItemStatus.Available });

        _context.DiningSessions.AddRange(
            new DiningSession { Session_Id = OpenSession, Location_Id = North, Menu_Id = DinnerMenu, Started_At = now.AddHours(-1) },
            new DiningSession { Session_Id = EndedSession, Location_Id = North, Menu_Id = DinnerMenu, Started_At = now.AddHours(-2), Ended_At = now.AddMinutes(-1) });

        _context.SessionParticipants.AddRange(
            new SessionParticipant { Participant_Id = 1, Session_Id = OpenSession, User_Id = GuestUser },
            new SessionParticipant { Participant_Id = 2, Session_Id = EndedSession, User_Id = GuestUser });

        _context.SaveChanges();
    }

    private void SignIn(int? userId, string role = "Customer")
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (userId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        }

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
        };
    }

    private Task<IActionResult> Record(int sessionId, int itemId, int seconds = 12) =>
        _controller.RecordView(new MenuItemViewCreateDTO { SessionId = sessionId, ItemId = itemId, ViewSeconds = seconds });

    private static int? StatusOf(IActionResult result) => (result as IStatusCodeActionResult)?.StatusCode;

    [Fact]
    public async Task RecordView_Participant_StoresViewWithSessionsMenuAndLocation()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var result = await Record(OpenSession, Salmon, seconds: 12);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        var view = _context.MenuItemViews.Single();
        view.Item_Id.Should().Be(Salmon);
        view.Session_Id.Should().Be(OpenSession);
        view.Menu_Id.Should().Be(DinnerMenu);
        view.Location_Id.Should().Be(North);
        view.User_Id.Should().Be(GuestUser);
        view.View_Seconds.Should().Be(12);
        view.Was_Available.Should().BeTrue();
        view.Viewed_At.Should().BeCloseTo(before.AddSeconds(-12), TimeSpan.FromSeconds(5),
            "Viewed_At is when the view started");
    }

    [Fact]
    public async Task RecordView_ShortView_IsStoredForReportsToIgnore()
    {
        // Act
        var result = await Record(OpenSession, Salmon, seconds: 2);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _context.MenuItemViews.Single().View_Seconds.Should().Be(2);
    }

    [Fact]
    public async Task RecordView_VeryLongView_IsCapped()
    {
        // Act
        await Record(OpenSession, Salmon, seconds: 5000);

        // Assert
        _context.MenuItemViews.Single().View_Seconds.Should().Be(ViewTrackingRules.MaxRecordedSeconds);
    }

    [Fact]
    public async Task RecordView_ItemNotOnTheSessionsMenu_ReturnsNotFound()
    {
        // Act - Lunch Special is on another menu at the same location, but not the session's menu
        var result = await Record(OpenSession, LunchSpecial);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        _context.MenuItemViews.Should().BeEmpty();
    }

    [Theory]
    [InlineData(SoldOut)]
    [InlineData(Retired)]
    public async Task RecordView_UnavailableItemOrAssignment_StoredAsUnavailable(int itemId)
    {
        // Act
        var result = await Record(OpenSession, itemId);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _context.MenuItemViews.Single().Was_Available.Should().BeFalse();
    }

    [Fact]
    public async Task RecordView_UnknownSession_ReturnsNotFound()
    {
        // Act
        var result = await Record(999, Salmon);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RecordView_EndedSession_ReturnsConflict()
    {
        // Act
        var result = await Record(EndedSession, Salmon);

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
        _context.MenuItemViews.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordView_CustomerNotInSession_ReturnsForbidden()
    {
        // Arrange
        SignIn(OtherCustomer);

        // Act
        var result = await Record(OpenSession, Salmon);

        // Assert
        StatusOf(result).Should().Be(StatusCodes.Status403Forbidden);
        _context.MenuItemViews.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordView_StaffNotInSession_IsAccepted()
    {
        // Arrange
        SignIn(StaffUser, role: "Staff");

        // Act
        var result = await Record(OpenSession, Salmon);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RecordView_TokenWithoutUserId_ReturnsUnauthorized()
    {
        // Arrange
        SignIn(null);

        // Act
        var result = await Record(OpenSession, Salmon);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }
}