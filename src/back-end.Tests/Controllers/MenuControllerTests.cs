using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using back_end.domain.enums;
using back_end.DTO.MenuDTO;
using back_end.DTO.MenuItems;

namespace back_end.Tests.Controllers;

public class MenuControllerTests : IDisposable
{
    private const int North = 1, South = 2;
    private const int MainMenu = 1, OldMenu = 2, SouthMenu = 3;
    private const int Salmon = 10, Toro = 11, SoldOut = 12, SeasonalItem = 13, SouthOnly = 15;
    private const int RawTag = 1, SpicyTag = 2;

    private readonly ApplicationDbContext _context;
    private readonly MenuController _controller;
    private readonly Mock<ILogger<MenuController>> _mockLogger;

    public MenuControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _mockLogger = new Mock<ILogger<MenuController>>();
        _controller = new MenuController(_context, _mockLogger.Object);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var location = new Locations
        {
            Location_Id = North,
            Name = "Test Location",
            Address_Primary = "123 Test St",
            City = "Test City",
            Province = "TC",
            Postal_Code = "T1T 1T1",
            Phone_Number = "123-456-7890"
        };

        var menu = new Menu
        {
            Menu_id = MainMenu,
            Name = "Main Menu",
            Description = "Main menu for testing",
            Start_time = new TimeOnly(8, 0),
            End_time = new TimeOnly(22, 0),
            Is_active = true
        };

        var category = new Category
        {
            Category_id = 1,
            Category_name = "Appetizers",
            Description = "Starters and appetizers"
        };

        _context.Locations.Add(location);
        _context.Menus.Add(menu);
        _context.Categories.Add(category);
        _context.SaveChanges();
    }

    private void SeedMenuItems()
    {
        _context.Locations.Add(new Locations
        {
            Location_Id = South,
            Name = "South Location",
            Address_Primary = "456 Test Ave",
            City = "Test City",
            Province = "TC",
            Postal_Code = "T2T 2T2",
            Phone_Number = "123-456-0000"
        });

        _context.Menus.AddRange(
            new Menu { Menu_id = OldMenu, Name = "Old Menu", Description = "Inactive", Is_active = false },
            new Menu { Menu_id = SouthMenu, Name = "South Menu", Description = "South only", Is_active = true });

        _context.MenuLocations.AddRange(
            new MenuLocations { Menu_Id = MainMenu, Location_Id = North },
            new MenuLocations { Menu_Id = OldMenu, Location_Id = North },
            new MenuLocations { Menu_Id = MainMenu, Location_Id = South },
            new MenuLocations { Menu_Id = SouthMenu, Location_Id = South });

        var category = _context.Categories.Find(1)!;
        category.child_limit = 3;
        category.toddler_limit = 0;

        _context.Tags.AddRange(
            new Tag { tag_id = RawTag, tag_name = "Raw", tag_color = "#FF0000" },
            new Tag { tag_id = SpicyTag, tag_name = "Spicy", tag_color = "#FFA500" });

        _context.MenuItems.AddRange(
            new Menu_Item { item_id = Salmon, Name = "Salmon", Description = "Salmon nigiri", Category_id = 1 },
            new Menu_Item { item_id = Toro, Name = "Toro", Description = "Add-on", Category_id = 1 },
            new Menu_Item { item_id = SoldOut, Name = "Sold Out", Description = "Sold out", Category_id = 1 },
            new Menu_Item { item_id = SeasonalItem, Name = "Seasonal", Description = "Seasonal", Category_id = 1 },
            new Menu_Item { item_id = SouthOnly, Name = "South Only", Description = "South menu item", Category_id = 1 });

        _context.MenuItemTags.Add(new MenuItemTag { Menu_item_id = Salmon, Tag_id = RawTag });

        _context.MenuItemAssignments.AddRange(
            // Defaults (Location_Id = null)
            new MenuItemAssignment { Menu_Id = MainMenu, Item_Id = Salmon, Price = 0m, Status = MenuItemStatus.Available, Adult_Limit = 4, Child_limit = 2, Tot_Limit = 0 },
            new MenuItemAssignment { Menu_Id = MainMenu, Item_Id = Toro, Price = 1.00m, Is_Add_On = true, Status = MenuItemStatus.Available },
            new MenuItemAssignment { Menu_Id = MainMenu, Item_Id = SoldOut, Status = MenuItemStatus.Unavailable },
            new MenuItemAssignment { Menu_Id = MainMenu, Item_Id = SeasonalItem, Status = MenuItemStatus.Seasonal },
            new MenuItemAssignment { Menu_Id = SouthMenu, Item_Id = SouthOnly, Status = MenuItemStatus.Available },
            // South overrides
            new MenuItemAssignment { Menu_Id = MainMenu, Item_Id = Salmon, Location_Id = South, Price = 1.50m, Status = MenuItemStatus.Available },
            new MenuItemAssignment { Menu_Id = MainMenu, Item_Id = Toro, Location_Id = South, Price = 1.00m, Is_Add_On = true, Status = MenuItemStatus.Unavailable });

        _context.SaveChanges();
    }

    // GetItemsByMenu returns anonymous objects; JSON is the simplest way to read them
    private static List<JsonElement> ItemsFrom(ActionResult<IEnumerable<object>> result)
    {
        result.Result.Should().BeOfType<OkObjectResult>();
        var value = ((OkObjectResult)result.Result!).Value;
        return JsonSerializer.SerializeToElement(value).EnumerateArray().ToList();
    }

    private static List<int> IdsOf(List<JsonElement> items) =>
        items.Select(i => i.GetProperty("itemId").GetInt32()).ToList();

    private static List<Menu> MenusFrom(ActionResult<IEnumerable<Menu>> result)
    {
        result.Result.Should().BeOfType<OkObjectResult>();
        return ((IEnumerable<Menu>)((OkObjectResult)result.Result!).Value!).ToList();
    }

    [Fact]
    public async Task GetAllMenu_ReturnsAllMenus()
    {
        // Act
        var result = await _controller.GetAllMenu();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var menus = okResult!.Value as List<Menu>;

        menus.Should().NotBeNull();
        menus!.Should().HaveCount(1);
        menus[0].Name.Should().Be("Main Menu");
    }

    [Fact]
    public async Task GetMenuByID_ValidId_ReturnsMenu()
    {
        // Act
        var result = await _controller.GetMenuByID(1);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var menu = okResult!.Value as Menu;

        menu.Should().NotBeNull();
        menu!.Menu_id.Should().Be(1);
        menu.Name.Should().Be("Main Menu");
    }

    [Fact]
    public async Task GetMenuByID_InvalidId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetMenuByID(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetAllMenu_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        _context.Menus.RemoveRange(_context.Menus);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetAllMenu();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var menus = okResult!.Value as List<Menu>;

        menus.Should().NotBeNull();
        menus!.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateMenuItem_DuplicateName_ReturnsConflict()
    {
        // Arrange
        _context.MenuItems.Add(new Menu_Item { item_id = Salmon, Name = "Salmon", Description = "x", Category_id = 1 });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.Create_Menu_Item(new MenuItemCreateDTO { Name = "Salmon", Category_Id = 1 });

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task CreateMenuItem_WithTags_AttachesKnownTagsAndIgnoresUnknown()
    {
        // Arrange
        _context.Tags.AddRange(
            new Tag { tag_id = RawTag, tag_name = "Raw", tag_color = "#FF0000" },
            new Tag { tag_id = SpicyTag, tag_name = "Spicy", tag_color = "#FFA500" });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.Create_Menu_Item(new MenuItemCreateDTO
        {
            Name = "Spicy Tuna",
            Category_Id = 1,
            Tag_Ids = [RawTag, SpicyTag, 999]
        });

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var dto = (MenuItemResponseDTO)((OkObjectResult)result).Value!;
        dto.Status.Should().Be(MenuItemStatus.Available);
        dto.Description.Should().BeEmpty();
        dto.Tags.Select(t => t.Name).Should().BeEquivalentTo(["Raw", "Spicy"]);
        _context.MenuItemTags.Count(t => t.Menu_item_id == dto.Item_Id).Should().Be(2);
    }

    [Fact]
    public async Task GetItemsByMenu_UnknownMenu_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetItemsByMenu(999, null);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetItemsByMenu_ReturnsOnlyAvailableItemsOnThatMenu()
    {
        // Arrange
        SeedMenuItems();

        // Act
        var ids = IdsOf(ItemsFrom(await _controller.GetItemsByMenu(MainMenu, null)));

        // Assert
        ids.Should().Contain([Salmon, Toro]);
        ids.Should().NotContain(SoldOut, "Unavailable assignments are hidden");
        ids.Should().NotContain(SeasonalItem, "only Available assignments are shown");
        ids.Should().NotContain(SouthOnly, "items on other menus are excluded");
    }

    [Fact]
    public async Task GetItemsByMenu_LocationOverrides_ApplyAtThatLocationOnly()
    {
        // Arrange
        SeedMenuItems();

        // Act
        var south = ItemsFrom(await _controller.GetItemsByMenu(MainMenu, South));
        var north = ItemsFrom(await _controller.GetItemsByMenu(MainMenu, North));

        // Assert: South's price override replaces the default (one row, not two)
        var salmon = south.Single(i => i.GetProperty("itemId").GetInt32() == Salmon);
        salmon.GetProperty("price").GetDecimal().Should().Be(1.50m);

        // Assert: South's "Unavailable" override hides Toro there, but not at North
        IdsOf(south).Should().NotContain(Toro);
        IdsOf(north).Should().Contain(Toro);
    }

    [Fact]
    public async Task GetItemsByMenu_ItemShape_HasLimitsTagsAndNoFullPrice()
    {
        // Arrange
        SeedMenuItems();

        // Act
        var items = ItemsFrom(await _controller.GetItemsByMenu(MainMenu, null));

        // Assert
        var salmon = items.Single(i => i.GetProperty("itemId").GetInt32() == Salmon);
        salmon.TryGetProperty("fullPrice", out _).Should().BeFalse("Price and Full_Price were merged into Price");
        salmon.GetProperty("category").GetString().Should().Be("Appetizers");
        salmon.GetProperty("isAddOn").GetBoolean().Should().BeFalse();

        // Toddler limit 0 falls back to the child limit
        salmon.GetProperty("itemLimits").GetProperty("toddler").GetInt32().Should().Be(2);
        salmon.GetProperty("categoryLimits").GetProperty("toddler").GetInt32().Should().Be(3);

        var tag = salmon.GetProperty("tags").EnumerateArray().Single();
        tag.GetProperty("name").GetString().Should().Be("Raw");
        tag.GetProperty("color").GetString().Should().Be("#FF0000");
    }

    [Fact]
    public async Task GetMenuByLocation_ReturnsOnlyActiveMenusAtThatLocation()
    {
        // Arrange
        SeedMenuItems();

        // Act
        var north = MenusFrom(await _controller.GetMenuByLocation(North)).Select(m => m.Menu_id);
        var south = MenusFrom(await _controller.GetMenuByLocation(South)).Select(m => m.Menu_id);

        // Assert
        north.Should().BeEquivalentTo([MainMenu], "Old Menu is inactive and South Menu is another location's");
        south.Should().BeEquivalentTo([MainMenu, SouthMenu]);
    }

    [Fact]
    public async Task UpdateMenu_PartialUpdate_ChangesOnlySentFields()
    {
        // Act
        var result = await _controller.Update_Menu(MainMenu, new MenuUpdateDTO { Is_Active = false });

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var dto = (MenuResponseDTO)((OkObjectResult)result).Value!;
        dto.Is_Active.Should().BeFalse();
        dto.Name.Should().Be("Main Menu");
        dto.Start_Time.Should().Be(new TimeOnly(8, 0));
        dto.End_Time.Should().Be(new TimeOnly(22, 0));
    }

    [Fact]
    public async Task UpdateMenu_RenameToAnotherMenusName_ReturnsConflict()
    {
        // Arrange
        SeedMenuItems();

        // Act
        var result = await _controller.Update_Menu(MainMenu, new MenuUpdateDTO { Name = "Old Menu" });

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
        _context.Menus.Single(m => m.Menu_id == MainMenu).Name.Should().Be("Main Menu");
    }

    [Fact]
    public async Task DeleteMenu_Existing_RemovesIt()
    {
        // Act
        var result = await _controller.Delete_Menu(MainMenu);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _context.Menus.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteMenu_WithDiningSession_ReturnsConflictAndKeepsMenu()
    {
        // Arrange
        _context.DiningSessions.Add(new DiningSession { Session_Id = 7, Menu_Id = MainMenu, Location_Id = North, Table_Id = 101, Started_At = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.Delete_Menu(MainMenu);

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
        _context.Menus.Should().ContainSingle(m => m.Menu_id == MainMenu);
        _context.DiningSessions.Should().ContainSingle(s => s.Session_Id == 7);
    }

    [Fact]
    public async Task DeleteMenu_WithOrderItem_ReturnsConflictAndKeepsMenu()
    {
        // Arrange
        _context.OrderItems.Add(new OrderItems { Menu_Id = MainMenu, Item_Id = Salmon, Order_Key = 1, Quantity = 1 });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.Delete_Menu(MainMenu);

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
        _context.Menus.Should().ContainSingle(m => m.Menu_id == MainMenu);
    }

    [Fact]
    public async Task CreateMenu_ReturnsMenuResponseDto()
    {
        // Act
        var result = await _controller.Create_Menu(new MenuCreateDTO { Name = "Dinner" });

        // Assert
        var dto = ((OkObjectResult)result).Value.Should().BeOfType<MenuResponseDTO>().Subject;
        dto.Name.Should().Be("Dinner");
        dto.Is_Active.Should().BeTrue();
        dto.Menu_Id.Should().BePositive();
    }

    [Theory]
    [InlineData(nameof(MenuController.Create_Menu), "adminOnly")]
    [InlineData(nameof(MenuController.Create_Menu_Item), "staffOnly")]
    [InlineData(nameof(MenuController.Update_Menu), "adminOnly")]
    [InlineData(nameof(MenuController.Delete_Menu), "adminOnly")]
    public void WriteEndpoints_RequirePolicy(string methodName, string policy)
    {
        // Act
        var attribute = typeof(MenuController).GetMethod(methodName)!.GetCustomAttribute<AuthorizeAttribute>();

        // Assert
        attribute.Should().NotBeNull();
        attribute!.Policy.Should().Be(policy);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}