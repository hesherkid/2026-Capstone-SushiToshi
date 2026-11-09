using System.Security.Claims;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Dtos;
using back_end.Services;

namespace back_end.Tests.Controllers;

// Update staff 403 tests are on StaffLocationLockTests, not repeated here.
public class MenuAssignmentControllerTests : IDisposable
{
    private const int Northside = 1;
    private const int Southside = 2;
    private const int StaffId = 1;
    private const int AdminId = 2;
    private const int DinnerMenu = 1;
    private const int LunchMenu = 2;
    private const int SalmonNigiri = 10;
    private const int MisoSoup = 20;
    private const int GreenTea = 30;
    private const int NewItem = 40;

    private readonly ApplicationDbContext _context;

    public MenuAssignmentControllerTests()
    {
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        _context.Locations.AddRange(
            new Locations { Location_Id = Northside, Name = "Northside" },
            new Locations { Location_Id = Southside, Name = "Southside" });
        _context.Users.AddRange(
            new User { User_id = StaffId, Email = "staff@example.com", Role = UserRoles.Staff, Location_id = Northside },
            new User { User_id = AdminId, Email = "admin@example.com", Role = UserRoles.Admin });
        _context.Menus.AddRange(
            new Menu { Menu_id = DinnerMenu, Name = "Dinner" },
            new Menu { Menu_id = LunchMenu, Name = "Lunch" });

        _context.MenuItemAssignments.AddRange(
            Assignment(DinnerMenu, SalmonNigiri, null, 3.50m),
            Assignment(DinnerMenu, SalmonNigiri, Northside, 4.00m),
            Assignment(DinnerMenu, SalmonNigiri, Southside, 4.25m),
            new MenuItemAssignment
            {
                Menu_Id = DinnerMenu,
                Item_Id = MisoSoup,
                Price = 0m,
                Adult_Limit = 0,
                Child_limit = 0,
                Senior_limit = 0,
                Tot_Limit = 0,
                Per_Order_Limit = 0,
                Over_Limit_Behavior = OverLimitBehavior.Block,
                Status = MenuItemStatus.Available,
            },
            Assignment(LunchMenu, GreenTea, null, 1.00m));
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    public void Dispose() => _context.Dispose();

    private static MenuItemAssignment Assignment(int menuId, int itemId, int? locationId, decimal price) => new()
    {
        Menu_Id = menuId,
        Item_Id = itemId,
        Location_Id = locationId,
        Price = price,
        Adult_Limit = 6,
        Child_limit = 4,
        Senior_limit = 5,
        Tot_Limit = 2,
        Per_Order_Limit = 10,
        Over_Limit_Behavior = OverLimitBehavior.Block,
        Is_Add_On = false,
        Status = MenuItemStatus.Available,
    };

    private MenuAssignmentController Controller(int userId, UserRoles role) =>
        new(_context, new Mock<ILogger<MenuAssignmentController>>().Object, new LocationAccessService(_context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Role, role.ToString()),
                    }, "TestAuth")),
                },
            },
        };

    private MenuAssignmentController Admin() => Controller(AdminId, UserRoles.Admin);
    private MenuAssignmentController Staff() => Controller(StaffId, UserRoles.Staff);

    private static MenuAssignmentCreate ValidCreate(int itemId = NewItem, int? locationId = null) => new()
    {
        Menu_Id = DinnerMenu,
        Item_Id = itemId,
        Location_Id = locationId,
        Price = 0m,
        Adult_Limit = 6,
        Child_Limit = 4,
        Senior_Limit = 5,
    };

    private async Task<MenuItemAssignment?> Reload(int menuId, int itemId, int? locationId)
    {
        _context.ChangeTracker.Clear();
        return await _context.MenuItemAssignments.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Menu_Id == menuId && a.Item_Id == itemId && a.Location_Id == locationId);
    }

    private static void ShouldBeStatus(IActionResult result, int status) =>
        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(status);

    [Fact]
    public async Task Create_SavesAndFillsInDefaults()
    {
        var data = ValidCreate();
        data.Over_Limit_Behavior = null;
        data.Tot_Limit = null;
        data.Per_Order_Limit = null;

        var result = await Admin().CreateMenuItemAssignment(data);

        var saved = result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<MenuItemAssignment>().Subject;
        saved.Over_Limit_Behavior.Should().Be(OverLimitBehavior.Block);
        saved.Tot_Limit.Should().Be(0);
        saved.Per_Order_Limit.Should().Be(0);
        saved.Location_Id.Should().BeNull();
        (await Reload(DinnerMenu, NewItem, null)).Should().NotBeNull();
    }

    [Fact]
    public async Task Create_SavesAllFields()
    {
        var data = ValidCreate();
        data.Price = 6.95m;
        data.Is_Add_on = true;
        data.Over_Limit_Behavior = OverLimitBehavior.Charge;
        data.Tot_Limit = 3;
        data.Per_Order_Limit = 2;
        data.Status = MenuItemStatus.Unavailable;

        var result = await Admin().CreateMenuItemAssignment(data);

        result.Should().BeOfType<OkObjectResult>();
        var saved = await Reload(DinnerMenu, NewItem, null);
        saved!.Price.Should().Be(6.95m);
        saved.Is_Add_On.Should().BeTrue();
        saved.Over_Limit_Behavior.Should().Be(OverLimitBehavior.Charge);
        saved.Tot_Limit.Should().Be(3);
        saved.Per_Order_Limit.Should().Be(2);
        saved.Status.Should().Be(MenuItemStatus.Unavailable);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0, 0, "Limits cannot be negative.")]
    [InlineData(4, 6, 0, 0, 0, "Child limit cannot exceed Adult limit.")]
    [InlineData(4, 4, 6, 0, 0, "Senior limit cannot exceed Adult limit.")]
    [InlineData(6, 4, 5, 5, 0, "Toddler limit cannot exceed Child limit.")]
    [InlineData(6, 4, 5, 0, -1, "Limits cannot be negative.")]
    public async Task Create_InvalidLimits_ReturnsBadRequest(int adult, int child, int senior, int tot, int perOrder, string message)
    {
        var data = ValidCreate();
        data.Adult_Limit = adult; data.Child_Limit = child; data.Senior_Limit = senior;
        data.Tot_Limit = tot; data.Per_Order_Limit = perOrder;

        var result = await Admin().CreateMenuItemAssignment(data);

        result.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should().Be(message);
        (await Reload(DinnerMenu, NewItem, null)).Should().BeNull();
    }

    [Fact]
    public async Task Create_ChargeWithZeroPrice_ReturnsBadRequest()
    {
        var data = ValidCreate();
        data.Over_Limit_Behavior = OverLimitBehavior.Charge;

        var result = await Admin().CreateMenuItemAssignment(data);

        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().Be("Items that charge over the limit need a full price greater than 0");
    }

    [Fact]
    public async Task Create_AddOnWithZeroPrice_ReturnsBadRequest()
    {
        var data = ValidCreate();
        data.Is_Add_on = true;

        var result = await Admin().CreateMenuItemAssignment(data);

        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().Be("Add-on items must have a price greater than 0");
    }

    [Fact]
    public async Task Update_MissingAssignment_ReturnsNotFound()
    {
        var result = await Admin().UpdateMenuItemAssignment(DinnerMenu, NewItem, new MenuAssignmentUpdateDto { Price = 1m }, locationId: null);

        result.Should().BeOfType<NotFoundObjectResult>().Which.Value.Should().Be("Menu Assignment was not found.");
    }

    [Fact]
    public async Task Update_OnlyOneField_KeepsAllOtherFields()
    {
        var result = await Admin().UpdateMenuItemAssignment(DinnerMenu, SalmonNigiri, new MenuAssignmentUpdateDto { Price = 3.75m }, locationId: null);

        result.Should().BeOfType<OkObjectResult>();
        var saved = await Reload(DinnerMenu, SalmonNigiri, null);
        saved!.Price.Should().Be(3.75m);
        saved.Adult_Limit.Should().Be(6);
        saved.Child_limit.Should().Be(4);
        saved.Senior_limit.Should().Be(5);
        saved.Tot_Limit.Should().Be(2);
        saved.Per_Order_Limit.Should().Be(10);
        saved.Over_Limit_Behavior.Should().Be(OverLimitBehavior.Block);
        saved.Is_Add_On.Should().BeFalse();
        saved.Status.Should().Be(MenuItemStatus.Available);
    }

    [Fact]
    public async Task Update_EveryField_SavesThem()
    {
        var update = new MenuAssignmentUpdateDto
        {
            Price = 5m,
            Adult_Limit = 8,
            Child_Limit = 6,
            Senior_Limit = 7,
            Tot_Limit = 3,
            Per_Order_Limit = 12,
            Over_Limit_Behavior = OverLimitBehavior.Charge,
            Is_Add_on = true,
            Status = MenuItemStatus.Unavailable,
        };

        var result = await Admin().UpdateMenuItemAssignment(DinnerMenu, SalmonNigiri, update, locationId: null);

        result.Should().BeOfType<OkObjectResult>();
        var saved = await Reload(DinnerMenu, SalmonNigiri, null);
        saved.Should().BeEquivalentTo(new
        {
            Price = 5m,
            Adult_Limit = 8,
            Child_limit = 6,
            Senior_limit = 7,
            Tot_Limit = 3,
            Per_Order_Limit = 12,
            Over_Limit_Behavior = OverLimitBehavior.Charge,
            Is_Add_On = true,
            Status = MenuItemStatus.Unavailable,
        });
    }

    [Fact]
    public async Task Update_Override_LeavesDefaultAndOtherLocationsAlone()
    {
        var result = await Admin().UpdateMenuItemAssignment(DinnerMenu, SalmonNigiri, new MenuAssignmentUpdateDto { Price = 9m }, locationId: Southside);

        result.Should().BeOfType<OkObjectResult>();
        (await Reload(DinnerMenu, SalmonNigiri, Southside))!.Price.Should().Be(9m);
        (await Reload(DinnerMenu, SalmonNigiri, null))!.Price.Should().Be(3.50m);
        (await Reload(DinnerMenu, SalmonNigiri, Northside))!.Price.Should().Be(4.00m);
    }

    [Fact]
    public async Task Update_StaffAtOwnLocation_IsAllowed()
    {
        var result = await Staff().UpdateMenuItemAssignment(DinnerMenu, SalmonNigiri, new MenuAssignmentUpdateDto { Price = 4.50m }, locationId: Northside);

        result.Should().BeOfType<OkObjectResult>();
        (await Reload(DinnerMenu, SalmonNigiri, Northside))!.Price.Should().Be(4.50m);
    }

    // Validation uses the merged values: child 7 is checked against the stored adult limit of 6
    [Fact]
    public async Task Update_ChildAboveStoredAdult_ReturnsBadRequest()
    {
        var result = await Admin().UpdateMenuItemAssignment(DinnerMenu, SalmonNigiri, new MenuAssignmentUpdateDto { Child_Limit = 7, Price = 8m }, locationId: null);

        result.Should().BeOfType<BadRequestObjectResult>().Which.Value.Should().Be("Child limit cannot exceed Adult limit.");
        var saved = await Reload(DinnerMenu, SalmonNigiri, null);
        saved!.Child_limit.Should().Be(4);
        saved.Price.Should().Be(3.50m);
    }

    [Fact]
    public async Task Update_ChargeOnFreeItem_ReturnsBadRequest()
    {
        var result = await Admin().UpdateMenuItemAssignment(DinnerMenu, MisoSoup,
            new MenuAssignmentUpdateDto { Over_Limit_Behavior = OverLimitBehavior.Charge }, locationId: null);

        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().Be("Items that charge over the limit need a full price greater than 0");
    }

    [Fact]
    public async Task Update_AddOnOnFreeItem_ReturnsBadRequest()
    {
        var result = await Admin().UpdateMenuItemAssignment(DinnerMenu, MisoSoup,
            new MenuAssignmentUpdateDto { Is_Add_on = true }, locationId: null);

        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().Be("Add-on items must have a price greater than 0");
    }

    [Fact]
    public async Task Update_ChargeWithNewPriceInSameRequest_IsAllowed()
    {
        var result = await Admin().UpdateMenuItemAssignment(DinnerMenu, MisoSoup,
            new MenuAssignmentUpdateDto { Over_Limit_Behavior = OverLimitBehavior.Charge, Price = 2.50m }, locationId: null);

        result.Should().BeOfType<OkObjectResult>();
        (await Reload(DinnerMenu, MisoSoup, null))!.Over_Limit_Behavior.Should().Be(OverLimitBehavior.Charge);
    }

    [Fact]
    public async Task Delete_DefaultAlsoRemovesEveryOverride()
    {
        var result = await Admin().DeleteMenuItemAssignment(DinnerMenu, SalmonNigiri, locationId: null);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be("Menu Assignment was deleted Successfully");
        _context.ChangeTracker.Clear();
        (await _context.MenuItemAssignments.CountAsync(a => a.Menu_Id == DinnerMenu && a.Item_Id == SalmonNigiri)).Should().Be(0);
        (await _context.MenuItemAssignments.CountAsync(a => a.Item_Id == MisoSoup)).Should().Be(1);
    }

    [Fact]
    public async Task Delete_OverrideRemovesOnlyThatLocation()
    {
        var result = await Admin().DeleteMenuItemAssignment(DinnerMenu, SalmonNigiri, locationId: Southside);

        result.Should().BeOfType<OkObjectResult>();
        (await Reload(DinnerMenu, SalmonNigiri, Southside)).Should().BeNull();
        (await Reload(DinnerMenu, SalmonNigiri, null)).Should().NotBeNull();
        (await Reload(DinnerMenu, SalmonNigiri, Northside)).Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_MissingAssignment_ReturnsNotFound()
    {
        var result = await Admin().DeleteMenuItemAssignment(DinnerMenu, NewItem, locationId: null);

        result.Should().BeOfType<NotFoundObjectResult>().Which.Value.Should().Be("Menu Assignment for deletion was not found");
    }

    [Fact]
    public async Task Delete_StaffRemovingDefault_ReturnsForbidden()
    {
        var result = await Staff().DeleteMenuItemAssignment(DinnerMenu, SalmonNigiri, locationId: null);

        ShouldBeStatus(result, StatusCodes.Status403Forbidden);
        ((ObjectResult)result).Value.Should().Be("Only admins can change the menu for every location.");
        (await Reload(DinnerMenu, SalmonNigiri, null)).Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_StaffAtAnotherLocation_ReturnsForbidden()
    {
        var result = await Staff().DeleteMenuItemAssignment(DinnerMenu, SalmonNigiri, locationId: Southside);

        ShouldBeStatus(result, StatusCodes.Status403Forbidden);
        ((ObjectResult)result).Value.Should().Be(ILocationAccessService.OtherLocationMessage);
        (await Reload(DinnerMenu, SalmonNigiri, Southside)).Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_StaffAtOwnLocation_IsAllowed()
    {
        var result = await Staff().DeleteMenuItemAssignment(DinnerMenu, SalmonNigiri, locationId: Northside);

        result.Should().BeOfType<OkObjectResult>();
        (await Reload(DinnerMenu, SalmonNigiri, Northside)).Should().BeNull();
    }

    [Fact]
    public async Task Copy_NameAlreadyUsed_ReturnsConflict()
    {
        var result = await Admin().CopyMenuAssignments(DinnerMenu, "Lunch");

        result.Should().BeOfType<ConflictObjectResult>().Which.Value.Should().Be("Menu Name Already Exist");
        (await _context.Menus.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Copy_CopiesEveryRowIncludingOverrides()
    {
        var result = await Admin().CopyMenuAssignments(DinnerMenu, "Winter");

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(new { name = "Winter", assignments_copied = 4 });

        _context.ChangeTracker.Clear();
        var newMenuId = (await _context.Menus.SingleAsync(m => m.Name == "Winter")).Menu_id;
        newMenuId.Should().NotBe(DinnerMenu);

        var copied = await Fields(newMenuId);
        var source = await Fields(DinnerMenu);
        copied.Should().BeEquivalentTo(source);
    }

    [Fact]
    public async Task Copy_LeavesSourceMenuUnchanged()
    {
        await Admin().CopyMenuAssignments(DinnerMenu, "Winter");

        _context.ChangeTracker.Clear();
        (await _context.MenuItemAssignments.CountAsync(a => a.Menu_Id == DinnerMenu)).Should().Be(4);
        (await _context.MenuItemAssignments.CountAsync(a => a.Menu_Id == LunchMenu)).Should().Be(1);
    }

    [Fact]
    public async Task Copy_EmptySource_CreatesEmptyMenu()
    {
        var result = await Admin().CopyMenuAssignments(sourceMenuId: 999, "Empty");

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(new { name = "Empty", assignments_copied = 0 });
        (await _context.Menus.AnyAsync(m => m.Name == "Empty")).Should().BeTrue();
    }

    // The fields of a menu item assignment that are copied when duplicating a menu
    private sealed record CopiedFields(
        int ItemId, int? LocationId, decimal Price, int Adult, int Child, int Senior,
        int Tot, int PerOrder, OverLimitBehavior Behavior, bool IsAddOn, MenuItemStatus Status);

    private Task<List<CopiedFields>> Fields(int menuId) =>
        _context.MenuItemAssignments.AsNoTracking()
            .Where(a => a.Menu_Id == menuId)
            .Select(a => new CopiedFields(
                a.Item_Id, a.Location_Id, a.Price, a.Adult_Limit, a.Child_limit, a.Senior_limit,
                a.Tot_Limit, a.Per_Order_Limit, a.Over_Limit_Behavior, a.Is_Add_On, a.Status))
            .ToListAsync();
}