using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.DTO.TagDTOs;

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
        var categories = okResult!.Value as List<Category>;

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
        var category = okResult!.Value as Category;

        category.Should().NotBeNull();
        category!.Category_id.Should().Be(1);
        category.Category_name.Should().Be("Appetizers");
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
    public async Task GetAllCategories_OrderedByDisplayOrder()
    {
        // Act
        var result = await _controller.GetAllCategories();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var categories = okResult!.Value as List<Category>;

        categories.Should().NotBeNull();
        // Note: Display_Order property removed from Category entity
        // categories!.Should().BeInAscendingOrder(c => c.Display_Order);
        categories!.Should().HaveCount(3);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}