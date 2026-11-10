using Xunit;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Dtos;

namespace back_end.Tests.Controllers;

public class MenuItemControllerTests : IDisposable
{
    private const int North = 1;
    private const int MainMenu = 1;
    private const int Salmon = 10;
    private const int RawTag = 1, SpicyTag = 2;

    private readonly ApplicationDbContext _context;
    private readonly MenuItemController _controller;

    public MenuItemControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _controller = new MenuItemController(_context, Mock.Of<ILogger<MenuController>>(), Mock.Of<IWebHostEnvironment>());

        _context.Categories.Add(new Category { Category_id = 1, Category_name = "Nigiri", Description = "Nigiri" });
        _context.Menus.Add(new Menu { Menu_id = MainMenu, Name = "Main Menu", Description = "Main", Is_active = true });
        _context.Tags.AddRange(
            new Tag { tag_id = RawTag, tag_name = "Raw", tag_color = "#FF0000" },
            new Tag { tag_id = SpicyTag, tag_name = "Spicy", tag_color = "#FFA500" });
        _context.MenuItems.Add(new Menu_Item { item_id = Salmon, Name = "Salmon", Description = "Salmon nigiri", Category_id = 1 });
        _context.MenuItemTags.Add(new MenuItemTag { Menu_item_id = Salmon, Tag_id = RawTag });
        _context.MenuItemAssignments.AddRange(
            new MenuItemAssignment { Menu_Id = MainMenu, Item_Id = Salmon, Price = 0m, Status = MenuItemStatus.Available, Adult_Limit = 4, Per_Order_Limit = 2 },
            new MenuItemAssignment { Menu_Id = MainMenu, Item_Id = Salmon, Location_Id = North, Price = 1.50m, Status = MenuItemStatus.Available });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task CreateMenuItem_DuplicateName_ReturnsConflict()
    {
        // Act
        var result = await _controller.CreateMenuItem(new MenuItemCreateDto { Name = "Salmon", CategoryId = 1 });

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Theory]
    [InlineData(" ", 1)]
    [InlineData("Spicy Tuna", 0)]
    public async Task CreateMenuItem_BlankNameOrNoCategory_ReturnsBadRequest(string name, int categoryId)
    {
        // Act
        var result = await _controller.CreateMenuItem(new MenuItemCreateDto { Name = name, CategoryId = categoryId });

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        _context.MenuItems.Should().ContainSingle("only the seeded item exists");
    }

    [Fact]
    public async Task CreateMenuItem_WithTags_AttachesKnownTagsAndIgnoresUnknown()
    {
        // Act
        var result = await _controller.CreateMenuItem(new MenuItemCreateDto
        {
            Name = "Spicy Tuna",
            CategoryId = 1,
            TagIds = [RawTag, SpicyTag, 999]
        });

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var dto = (MenuItemResponseDto)((OkObjectResult)result).Value!;
        dto.Status.Should().Be(MenuItemStatus.Available);
        dto.Description.Should().BeEmpty();
        dto.Tags.Select(t => t.Name).Should().BeEquivalentTo(["Raw", "Spicy"]);
        _context.MenuItemTags.Count(t => t.Menu_item_id == dto.ItemId).Should().Be(2);
    }

    [Fact]
    public async Task GetMenuItem_ReturnsTheItemWithItsAssignmentsAndMenuNames()
    {
        // Act
        var result = await _controller.GetMenuItem(Salmon);

        // Assert
        var dto = result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<MenuItemWithAssignmentsDto>().Subject;
        dto.ItemId.Should().Be(Salmon);
        dto.Tags.Select(t => t.Name).Should().BeEquivalentTo(["Raw"]);
        dto.MenuAssignments.Should().HaveCount(2);
        dto.MenuAssignments.Should().AllSatisfy(a => a.MenuName.Should().Be("Main Menu"));

        var defaultRow = dto.MenuAssignments.Single(a => a.LocationId == null);
        defaultRow.AdultLimit.Should().Be(4);
        defaultRow.PerOrderLimit.Should().Be(2);
        dto.MenuAssignments.Single(a => a.LocationId == North).Price.Should().Be(1.50m);
    }

    [Fact]
    public async Task UpdateMenuItem_WithTagIds_ReturnsAllOfTheItemsTags()
    {
        // Act
        var result = await _controller.UpdateMenuItem(Salmon, new MenuItemUpdateDto { TagIds = [SpicyTag] });

        // Assert
        var dto = result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<MenuItemResponseDto>().Subject;
        dto.Tags.Select(t => t.Name).Should().BeEquivalentTo(["Raw", "Spicy"]);
        dto.Tags.Single(t => t.TagId == SpicyTag).ColorCode.Should().Be("#FFA500");
    }

    [Fact]
    public async Task RemoveTagToMenu_ReturnsTheItemWithoutThatTag()
    {
        // Act
        var result = await _controller.RemoveTagToMenu(Salmon, RawTag);

        // Assert
        var dto = result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<MenuItemResponseDto>().Subject;
        dto.Tags.Should().BeEmpty();
    }

    [Theory]
    [InlineData(nameof(MenuItemController.CreateMenuItem))]
    [InlineData(nameof(MenuItemController.UpdateMenuItem))]
    [InlineData(nameof(MenuItemController.UploadItemImage))]
    [InlineData(nameof(MenuItemController.DeleteMenuItem))]
    [InlineData(nameof(MenuItemController.UpdateItemStatus))]
    [InlineData(nameof(MenuItemController.AddTagToMenuItem))]
    [InlineData(nameof(MenuItemController.RemoveTagToMenu))]
    public void WriteEndpoints_RequireStaff(string methodName)
    {
        // Act
        var attribute = typeof(MenuItemController).GetMethod(methodName)!.GetCustomAttribute<AuthorizeAttribute>();

        // Assert
        attribute.Should().NotBeNull();
        attribute!.Policy.Should().Be("staffOnly");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
