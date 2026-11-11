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
using Microsoft.AspNetCore.Http;


namespace back_end.Tests.Controllers;

public class MenuItemControllerTests : IDisposable
{
    private const int North = 1;
    private const int MainMenu = 1;
    private const int Salmon = 10;
    private const int Tuna = 20; // seeded only by SeedTuna(). No menu assignments, so it can be deleted
    private const int RawTag = 1, SpicyTag = 2;
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly ApplicationDbContext _context;
    private readonly MenuItemController _controller;

    // Required for image upload tests
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "menuitem-tests-" + Guid.NewGuid());
    private string BackEndRoot => Path.Combine(_tempRoot, "back-end");
    private string ImageFolder => Path.Combine(_tempRoot, "front-end", "public", "menu-items");


    public MenuItemControllerTests()
    {
        _context = new ApplicationDbContext(Options());
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

    private DbContextOptions<ApplicationDbContext> Options() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

    private void SeedTuna(string? imageUrl = null)
    {
        _context.MenuItems.Add(new Menu_Item { item_id = Tuna, Name = "Bluefin Tuna", Description = "Tuna", Category_id = 2, image_url = imageUrl });
        _context.MenuItemTags.Add(new MenuItemTag { Menu_item_id = Tuna, Tag_id = SpicyTag });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    // Helper to create a controller with a temporary content root
    private MenuItemController ControllerWithTempRoot(ApplicationDbContext? context = null)
    {
        Directory.CreateDirectory(BackEndRoot);
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(BackEndRoot);
        return new MenuItemController(context ?? _context, Mock.Of<ILogger<MenuController>>(), env.Object);
    }

    // Helper to create a mock IFormFile for testing file uploads
    private static IFormFile MakeFile(string fileName, byte[]? content = null, long? reportedLength = null)
    {
        content ??= [1, 2, 3, 4];
        return new FormFile(new MemoryStream(content), 0, reportedLength ?? content.Length, "file", fileName);
    }

    private Menu_Item ReadItem(int id) =>
        _context.MenuItems.AsNoTracking().Include(mi => mi.MenuItemTags).Single(mi => mi.item_id == id);


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
    public async Task CreateMenuItem_NoTags_SavesItemWithImageUrlAndNoTags()
    {
        // Act
        var result = await _controller.CreateMenuItem(new MenuItemCreateDto
        {
            Name = "Ebi",
            Description = "Shrimp nigiri",
            CategoryId = 1,
            ItemImageUrl = "/menu-items/ebi.jpg"
        });

        // Assert
        var dto = result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<MenuItemResponseDto>().Subject;
        dto.Tags.Should().BeEmpty();
        var saved = ReadItem(dto.ItemId);
        saved.Description.Should().Be("Shrimp nigiri");
        saved.image_url.Should().Be("/menu-items/ebi.jpg");
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
    public async Task GetMenuItem_UnknownId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetMenuItem(999);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ListMenuItems_OrdersByCategoryThenName_WithTags()
    {
        // Arrange
        SeedTuna();
        _context.MenuItems.Add(new Menu_Item { item_id = 30, Name = "Udon", Description = "Sweet shrimp", Category_id = 1 });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _controller.ListMenuItems();

        // Assert
        var list = result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeAssignableTo<List<MenuItemResponseDto>>().Subject;
        list.Select(i => i.Name).Should().Equal("Salmon", "Udon", "Bluefin Tuna");
        list.Single(i => i.ItemId == Salmon).Tags.Select(t => t.Name).Should().Equal("Raw");
        list.Single(i => i.ItemId == Tuna).Tags.Single().ColorCode.Should().Be("#FFA500");
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
    public async Task UpdateMenuItem_AllFields_PersistEveryChange()
    {
        // Act
        var result = await _controller.UpdateMenuItem(Salmon, new MenuItemUpdateDto
        {
            Name = "King Salmon",
            Description = "Premium",
            CategoryId = 2,
            ItemImageUrl = "/menu-items/king.jpg",
            Status = MenuItemStatus.Seasonal
        });

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var saved = ReadItem(Salmon);
        saved.Name.Should().Be("King Salmon");
        saved.Description.Should().Be("Premium");
        saved.Category_id.Should().Be(2);
        saved.image_url.Should().Be("/menu-items/king.jpg");
        saved.Status.Should().Be(MenuItemStatus.Seasonal);
        saved.MenuItemTags.Should().ContainSingle("no TagIds were sent, so tags are untouched");
    }

    [Fact]
    public async Task UpdateMenuItem_OnlyDescription_OtherFieldsUnchanged()
    {
        // Act
        var result = await _controller.UpdateMenuItem(Salmon, new MenuItemUpdateDto { Description = "New text", TagIds = [] });

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var saved = ReadItem(Salmon);
        saved.Description.Should().Be("New text");
        saved.Name.Should().Be("Salmon");
        saved.Category_id.Should().Be(1);
        saved.Status.Should().Be(MenuItemStatus.Available);
    }

    [Fact]
    public async Task UpdateMenuItem_UnknownId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.UpdateMenuItem(999, new MenuItemUpdateDto { Name = "Ghost" });

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateItemStatus_StatusPersists_ReturnsItemWithTags()
    {
        // Act
        var result = await _controller.UpdateItemStatus(Salmon, MenuItemStatus.Unavailable);

        // Assert
        var dto = result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<MenuItemResponseDto>().Subject;
        dto.Status.Should().Be(MenuItemStatus.Unavailable);
        dto.Tags.Select(t => t.Name).Should().Equal("Raw");
        ReadItem(Salmon).Status.Should().Be(MenuItemStatus.Unavailable);

    }

    [Fact]
    public async Task UpdateItemStatus_UnknownId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.UpdateItemStatus(999, MenuItemStatus.Unavailable);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetItemTags_ReturnsTagsSortedByName()
    {
        _context.Tags.Add(new Tag { tag_id = 3, tag_name = "Al Fresco", tag_color = "#000000" });
        _context.MenuItemTags.Add(new MenuItemTag { Menu_item_id = Salmon, Tag_id = 3 });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _controller.GetItemTags(Salmon, MenuItemStatus.Available);

        // Assert
        var tags = result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeAssignableTo<List<TagResponseDto>>().Subject;
        tags.Select(t => t.Name).Should().Equal("Al Fresco", "Raw");
        tags.Select(t => t.TagId).Should().Equal(3, RawTag);
    }

    [Fact]
    public async Task GetItemTags_UnknownId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetItemTags(999, MenuItemStatus.Available);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AddTagToMenuItem_NewTag_ReturnsBothTags()
    {
        // Act
        var result = await _controller.AddTagToMenuItem(Salmon, SpicyTag);

        // Assert
        var dto = result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<MenuItemResponseDto>().Subject;
        dto.Tags.Select(t => t.Name).Should().BeEquivalentTo(["Raw", "Spicy"]);
        _context.MenuItemTags.Count(t => t.Menu_item_id == Salmon).Should().Be(2);
    }

    [Fact]
    public async Task AddTagToMenuItem_TagAlreadyAssigned_ReturnsConflict()
    {
        // Act
        var result = await _controller.AddTagToMenuItem(Salmon, RawTag);

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
        _context.MenuItemTags.Count(t => t.Menu_item_id == Salmon).Should().Be(1);
    }

    [Fact]
    public async Task AddTagToMenuItem_UnknownItem_ReturnsNotFound()
    {
        // Act
        var result = await _controller.AddTagToMenuItem(999, SpicyTag);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AddTagToMenuItem_UnknownTag_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.AddTagToMenuItem(Salmon, 999);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
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

    [Fact]
    public async Task RemoveTagToMenu_UnknownItem_ReturnsNotFound()
    {
        // Act
        var result = await _controller.RemoveTagToMenu(999, RawTag);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RemoveTagToMenu_UnknownTag_ReturnsNotFound()
    {
        // Act
        var result = await _controller.RemoveTagToMenu(Salmon, 999);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteMenuItem_UnknownId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.DeleteMenuItem(999);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteMenuItem_AssignedToAMenu_ReturnsConflictAndKeepsItem()
    {
        // Act
        var result = await _controller.DeleteMenuItem(Salmon);

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
        _context.MenuItems.Any(mi => mi.item_id == Salmon).Should().BeTrue();
        _context.MenuItemTags.Any(t => t.Menu_item_id == Salmon).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteMenuItem_NoImage_RemovesItemAndItsTags()
    {
        // Arrange
        SeedTuna();

        // Act
        var result = await _controller.DeleteMenuItem(Tuna);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _context.MenuItems.Any(mi => mi.item_id == Tuna).Should().BeFalse();
        _context.MenuItemTags.Any(t => t.Menu_item_id == Tuna).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteMenuItem_WithImage_RemovesItemAndItsTags()
    {
        // Arrange
        SeedTuna("/menu-items/tuna.jpg");
        var controller = ControllerWithTempRoot();
        Directory.CreateDirectory(ImageFolder);
        var imagePath = Path.Combine(ImageFolder, "tuna.jpg");
        File.WriteAllBytes(imagePath, [1, 2, 3]);

        // Act
        var result = await controller.DeleteMenuItem(Tuna);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        File.Exists(imagePath).Should().BeFalse();
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

    [Fact]
    public async Task UploadItemImage_UnknownItem_ReturnsNotFound()
    {
        // Act
        var result = await _controller.UploadItemImage(999, MakeFile("a.jpg"), CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UploadItemImage_NullOrEmptyFile_ReturnsBadRequest()
    {
        // Act
        var nullResult = await _controller.UploadItemImage(Salmon, null!, CancellationToken.None);
        var emptyResult = await _controller.UploadItemImage(Salmon, MakeFile("a.jpg", []), CancellationToken.None);

        // Assert
        nullResult.Should().BeOfType<BadRequestObjectResult>();
        emptyResult.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UploadItemImage_NoExtension_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.UploadItemImage(Salmon, MakeFile("photo"), CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UploadItemImage_Valid_SavesFileAndStoresUrlWithUnderscores()
    {
        // Arrange
        SeedTuna();
        var controller = ControllerWithTempRoot();

        // Act
        var result = await controller.UploadItemImage(Tuna, MakeFile("my photo.png"), CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var url = ReadItem(Tuna).image_url!;
        url.Should().StartWith("/menu-items/Bluefin_Tuna_").And.EndWith(".png").And.NotContain(" ");
        File.Exists(Path.Combine(ImageFolder, Path.GetFileName(url))).Should().BeTrue();
    }

    [Fact]
    public async Task UploadItemImage_ReplacingAnImage_DeletesTheOldFile()
    {
        // Arrange
        SeedTuna("/menu-items/old.jpg");
        var controller = ControllerWithTempRoot();
        Directory.CreateDirectory(ImageFolder);
        var oldPath = Path.Combine(ImageFolder, "old.jpg");
        File.WriteAllBytes(oldPath, [9, 9, 9]);

        // Act
        var result = await controller.UploadItemImage(Tuna, MakeFile("new.jpg"), CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        File.Exists(oldPath).Should().BeFalse();
        ReadItem(Tuna).image_url.Should().NotBe("/menu-items/old.jpg");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
