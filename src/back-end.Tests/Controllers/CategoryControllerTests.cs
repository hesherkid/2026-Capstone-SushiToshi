using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.DTO.Category;
using Microsoft.AspNetCore.Authorization;
using back_end.domain.enums;

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
        category!.Category_Id.Should().Be(1);
        category.Category_Name.Should().Be("Appetizers");
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
            Adult_Limit = 2,
            Child_Limit = 1,
            Senior_Limit = 2,
            Toddler_Limit = 1,
            Per_Order_Limit = 30,
            Over_Limit_Behavior = OverLimitBehavior.Charge
        };

        var result = await _controller.CreateCategory(request);

        result.Should().BeOfType<OkObjectResult>();
        var dto = (result as OkObjectResult)!.Value as CategoryResponse;

        dto.Should().NotBeNull();
        dto!.Category_Id.Should().BePositive();
        dto.Category_Name.Should().Be("Sashimi");
        dto.Adult_Limit.Should().Be(2);
        dto.Child_Limit.Should().Be(1);
        dto.Senior_Limit.Should().Be(2);
        dto.Toddler_Limit.Should().Be(1);
        dto.Per_Order_Limit.Should().Be(30);
        dto.Over_Limit_Behavior.Should().Be(OverLimitBehavior.Charge);
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

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}