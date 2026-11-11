using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.Dtos;
using Microsoft.AspNetCore.Authorization;
using back_end.domain.enums;
using Microsoft.AspNetCore.Http;

namespace back_end.Tests.Controllers;

public class CategoryControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly CategoryController _controller;

    public CategoryControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        var mockLogger = new Mock<ILogger<CategoryController>>();
        _controller = new CategoryController(_context, mockLogger.Object);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var categories = new List<Category>
        {
            new Category
            {
                Category_id = 1,
                Category_name = "Appetizers",
                Description = "Starters"
            },
            new Category
            {
                Category_id = 2,
                Category_name = "Main Courses",
                Description = "Main dishes"
            },
            new Category
            {
                Category_id = 3,
                Category_name = "Desserts",
                Description = "Sweet treats"
            }
        };

        _context.Categories.AddRange(categories);
        _context.SaveChanges();
    }

    private const int LimitedId = 10;

    // Helper methods to add and reload categories with limits
    // Category with limits set, so partial updates can prove values are kept
    private Category AddLimitedCategory()
    {
        var category = new Category
        {
            Category_id = LimitedId,
            Category_name = "Nigiri",
            Description = "Fish on rice",
            adult_limit = 6,
            child_limit = 4,
            senior_limit = 5,
            toddler_limit = 2,
            per_order_limit = 20,
            over_limit_behavior = OverLimitBehavior.Block
        };
        _context.Categories.Add(category);
        _context.SaveChanges();
        return category;
    }

    private void AddMenuItems(int categoryId, int count, int startId)
    {
        for (var i = 0; i < count; i++)
        {
            _context.MenuItems.Add(new Menu_Item
            {
                item_id = startId + i,
                Name = $"Item {startId + i}",
                Description = "Test item",
                Category_id = categoryId
            });
        }
        _context.SaveChanges();
    }

    private static CreateCategory ValidCreate(string name = "Sashimi") => new()
    {
        Name = name,
        Description = "Raw fish",
        AdultLimit = 6,
        ChildLimit = 4,
        SeniorLimit = 5
    };

    private async Task<Category> ReloadLimitedCategory()
    {
        _context.ChangeTracker.Clear();
        return await _context.Categories.AsNoTracking().SingleAsync(c => c.Category_id == LimitedId);
    }

    [Fact]
    public async Task GetAllCategories_ReturnsAllCategories()
    {
        // Act
        var result = await _controller.GetAllCategories();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var categories = okResult!.Value as List<CategoryResponse>;

        categories.Should().NotBeNull();
        categories!.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetCategoryById_ValidId_ReturnsCategory()
    {
        // Act
        var result = await _controller.GetCategoryById(1);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var category = okResult!.Value as CategoryResponse;

        category.Should().NotBeNull();
        category!.CategoryId.Should().Be(1);
        category.CategoryName.Should().Be("Appetizers");
    }

    [Fact]
    public async Task GetCategoryById_InvalidId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetCategoryById(999);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateCategory_ReturnsDtoWithLimits()
    {
        var request = new CreateCategory
        {
            Name = "Sashimi",
            Description = "Raw fish",
            AdultLimit = 2,
            ChildLimit = 1,
            SeniorLimit = 2,
            ToddlerLimit = 1,
            PerOrderLimit = 30,
            OverLimitBehavior = OverLimitBehavior.Charge
        };

        var result = await _controller.CreateCategory(request);

        result.Should().BeOfType<OkObjectResult>();
        var dto = (result as OkObjectResult)!.Value as CategoryResponse;

        dto.Should().NotBeNull();
        dto!.CategoryId.Should().BePositive();
        dto.CategoryName.Should().Be("Sashimi");
        dto.AdultLimit.Should().Be(2);
        dto.ChildLimit.Should().Be(1);
        dto.SeniorLimit.Should().Be(2);
        dto.ToddlerLimit.Should().Be(1);
        dto.PerOrderLimit.Should().Be(30);
        dto.OverLimitBehavior.Should().Be(OverLimitBehavior.Charge);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0, 0, "Limits cannot be negative.")]
    [InlineData(0, 0, 0, 0, -1, "Limits cannot be negative.")]
    [InlineData(4, 5, 4, 0, 0, "Child limit cannot exceed Adult limit.")]
    [InlineData(4, 0, 4, 0, 0, "Child limit cannot exceed Adult limit.")] // child 0 = unlimited, adult capped
    [InlineData(4, 4, 5, 0, 0, "Senior limit cannot exceed Adult limit.")]
    [InlineData(6, 3, 6, 4, 0, "Toddler limit cannot exceed Child limit.")]
    public async Task CreateCategory_InvalidLimits_ReturnsBadRequest_SavesNothing(
        int adult, int child, int senior, int toddler, int perOrder, string expectedMessage)
    {
        var request = new CreateCategory
        {
            Name = "Invalid",
            Description = "Should not be saved",
            AdultLimit = adult,
            ChildLimit = child,
            SeniorLimit = senior,
            ToddlerLimit = toddler,
            PerOrderLimit = perOrder
        };

        var result = await _controller.CreateCategory(request);

        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().Be(expectedMessage);
        (await _context.Categories.CountAsync()).Should().Be(3);
    }

    [Theory]
    [InlineData(0, 10, 10, 0)] // adult unlimited allows any child/senior value
    [InlineData(6, 6, 6, 6)]   // equal limits are allowed
    [InlineData(6, 4, 5, 0)]   // toddler 0 inherits child limit
    public async Task CreateCategory_BoundaryLimitsAreAccepted(int adult, int child, int senior, int toddler)
    {
        var request = new CreateCategory
        {
            Name = "Boundary",
            Description = "Edge values",
            AdultLimit = adult,
            ChildLimit = child,
            SeniorLimit = senior,
            ToddlerLimit = toddler
        };

        var result = await _controller.CreateCategory(request);

        result.Should().BeOfType<OkObjectResult>();
        (await _context.Categories.CountAsync()).Should().Be(4);
    }

    [Fact]
    public async Task CreateCategory_DuplicateName_ReturnsConflict_SavesNothing()
    {
        var result = await _controller.CreateCategory(ValidCreate("Appetizers"));

        result.Should().BeOfType<ConflictObjectResult>();
        (await _context.Categories.CountAsync(c => c.Category_name == "Appetizers")).Should().Be(1);
    }

    [Fact]
    public async Task CreateCategory_OptionalFieldsOmitted_UsesDefaults()
    {
        var request = new CreateCategory
        {
            Name = "Rolls",
            Description = null,
            AdultLimit = 0,
            ChildLimit = 0,
            SeniorLimit = 0
        };

        var result = await _controller.CreateCategory(request);

        var dto = result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<CategoryResponse>().Subject;
        dto.Description.Should().BeEmpty();
        dto.ToddlerLimit.Should().Be(0);
        dto.PerOrderLimit.Should().Be(0);
        dto.OverLimitBehavior.Should().Be(OverLimitBehavior.Block);

        var saved = await _context.Categories.AsNoTracking().SingleAsync(c => c.Category_id == dto.CategoryId);
        saved.Description.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateCategory_InvalidId_ReturnsNotFound()
    {
        var result = await _controller.UpdateCategory(999, new UpdateCategory { Description = "Nope" });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateCategory_OnlyDescription_KeepsOtherValues()
    {
        AddLimitedCategory();

        var result = await _controller.UpdateCategory(LimitedId, new UpdateCategory { Description = "Updated" });

        var dto = result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<CategoryResponse>().Subject;
        dto.Description.Should().Be("Updated");
        dto.CategoryName.Should().Be("Nigiri");
        dto.AdultLimit.Should().Be(6);
        dto.ChildLimit.Should().Be(4);
        dto.SeniorLimit.Should().Be(5);
        dto.ToddlerLimit.Should().Be(2);
        dto.PerOrderLimit.Should().Be(20);
        dto.OverLimitBehavior.Should().Be(OverLimitBehavior.Block);
    }

    [Fact]
    public async Task UpdateCategory_AllFields_PersistsChanges()
    {
        AddLimitedCategory();
        var request = new UpdateCategory
        {
            Name = "Premium Nigiri",
            Description = "Chef's selection",
            AdultLimit = 8,
            ChildLimit = 5,
            SeniorLimit = 7,
            ToddlerLimit = 3,
            PerOrderLimit = 40,
            OverLimitBehavior = OverLimitBehavior.Charge
        };

        var result = await _controller.UpdateCategory(LimitedId, request);

        result.Should().BeOfType<OkObjectResult>();
        var saved = await ReloadLimitedCategory();
        saved.Category_name.Should().Be("Premium Nigiri");
        saved.Description.Should().Be("Chef's selection");
        saved.adult_limit.Should().Be(8);
        saved.child_limit.Should().Be(5);
        saved.senior_limit.Should().Be(7);
        saved.toddler_limit.Should().Be(3);
        saved.per_order_limit.Should().Be(40);
        saved.over_limit_behavior.Should().Be(OverLimitBehavior.Charge);
    }

    [Theory]
    [InlineData(null, 8, null, null, "Child limit cannot exceed Adult limit.")]   // child raised above stored adult
    [InlineData(3, null, null, null, "Child limit cannot exceed Adult limit.")]   // adult lowered below stored child
    [InlineData(null, null, 7, null, "Senior limit cannot exceed Adult limit.")]
    [InlineData(null, 1, null, null, "Toddler limit cannot exceed Child limit.")] // child lowered below stored toddler
    [InlineData(null, null, null, -1, "Limits cannot be negative.")]
    public async Task UpdateCategory_LimitsInvalidAfterMerge_ReturnsBadRequest_ChangesNothing(
        int? adult, int? child, int? senior, int? toddler, string expectedMessage)
    {
        AddLimitedCategory();
        var request = new UpdateCategory
        {
            Name = "Should not be saved",
            AdultLimit = adult,
            ChildLimit = child,
            SeniorLimit = senior,
            ToddlerLimit = toddler
        };

        var result = await _controller.UpdateCategory(LimitedId, request);

        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().Be(expectedMessage);

        var saved = await ReloadLimitedCategory();
        saved.Category_name.Should().Be("Nigiri");
        saved.adult_limit.Should().Be(6);
        saved.child_limit.Should().Be(4);
        saved.senior_limit.Should().Be(5);
        saved.toddler_limit.Should().Be(2);
    }

    [Fact]
    public async Task DeleteCategory_WithMenuItems_ReturnsConflict()
    {
        _context.MenuItems.Add(new Menu_Item
        {
            item_id = 100,
            Name = "Miso Soup",
            Description = "Classic miso broth",
            Category_id = 1
        });
        _context.SaveChanges();

        var result = await _controller.DeleteCategory(1);

        result.Should().BeOfType<ConflictObjectResult>();
        (await _context.Categories.AnyAsync(c => c.Category_id == 1)).Should().BeTrue();
    }

    [Fact]
    public async Task GetMenuItemsByCategory_ReturnsItemDtosWithTheirTags()
    {
        // Arrange
        _context.Tags.Add(new Tag { tag_id = 5, tag_name = "Popular", tag_color = "#C01E2E" });
        _context.MenuItems.Add(new Menu_Item { item_id = 100, Name = "Miso Soup", Description = "Classic miso broth", Category_id = 1 });
        _context.MenuItemTags.Add(new MenuItemTag { Menu_item_id = 100, Tag_id = 5 });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        // Act
        var result = await _controller.GetMenuItemsByCategory(1);

        // Assert
        var items = result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<List<MenuItemResponseDto>>().Subject;
        var miso = items.Single();
        miso.ItemId.Should().Be(100);
        miso.CategoryId.Should().Be(1);
        miso.Tags.Single().Name.Should().Be("Popular");
    }

    [Fact]
    public async Task DeleteCategory_Empty_ReturnsOk()
    {
        var result = await _controller.DeleteCategory(3);

        result.Should().BeOfType<OkObjectResult>();
        (await _context.Categories.AnyAsync(c => c.Category_id == 3)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteCategory_InvalidId_ReturnsNotFound()
    {
        var result = await _controller.DeleteCategory(999);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Theory]
    [InlineData(2)]   // category exists, no items
    [InlineData(999)] // category does not exist
    public async Task GetMenuItemsByCategory_NoItems_ReturnsNotFound(int categoryId)
    {
        var result = await _controller.GetMenuItemsByCategory(categoryId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetMenuItemsByCategory_ReturnsOnlyItemsInThatCategory()
    {
        AddMenuItems(categoryId: 1, count: 3, startId: 200);
        AddMenuItems(categoryId: 2, count: 2, startId: 300);

        var result = await _controller.GetMenuItemsByCategory(1);

        var items = result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeAssignableTo<List<MenuItemResponseDto>>().Subject;
        items.Should().HaveCount(3);
        items.Should().OnlyContain(m => m.CategoryId == 1);
    }

    [Fact]
    public async Task GetMenuItemsByCategory_SkipPastEnd_ReturnsNotFound()
    {
        AddMenuItems(categoryId: 1, count: 5, startId: 200);

        var result = await _controller.GetMenuItemsByCategory(1, skip: 5);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Theory]
    [InlineData(nameof(CategoryController.CreateCategory))]
    [InlineData(nameof(CategoryController.UpdateCategory))]
    [InlineData(nameof(CategoryController.DeleteCategory))]
    [InlineData(nameof(CategoryController.GetMenuItemsByCategory))]
    public void AdminEndpoints_RequireAdminOnlyPolicy(string methodName)
    {
        var attribute = typeof(CategoryController).GetMethod(methodName)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        attribute.Policy.Should().Be("adminOnly");
    }

    [Theory]
    [InlineData(nameof(CategoryController.GetAllCategories))]
    [InlineData(nameof(CategoryController.GetCategoryById))]
    public void BrowseEndpoints_ArePublic(string methodName)
    {
        typeof(CategoryController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Should().BeEmpty();
        typeof(CategoryController).GetMethod(methodName)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Should().BeEmpty();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}