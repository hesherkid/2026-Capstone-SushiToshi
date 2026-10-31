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

public class TagControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly TagController _controller;

    public TagControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        var mockTagLogger = new Mock<ILogger<TagController>>();
        _controller = new TagController(_context, mockTagLogger.Object);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var tags = new List<Tag>
        {
            new Tag
            {
                tag_id = 1,
                tag_name = "Vegetarian",
                tag_color = "#00FF00"
            },
            new Tag
            {
                tag_id = 2,
                tag_name = "Spicy",
                tag_color = "#FF0000"
            },
            new Tag
            {
                tag_id = 3,
                tag_name = "Gluten-Free",
                tag_color = "#0000FF"
            }
        };

        _context.Tags.AddRange(tags);
        _context.SaveChanges();
    }

    private static string? Detail(IActionResult result)
    {
        var value = (result as ObjectResult)?.Value;
        return value?.GetType().GetProperty("detail")?.GetValue(value) as string;
    }

    [Fact]
    public async Task GetAllTags_ReturnsAllTags()
    {
        // Act
        var result = await _controller.GetTags();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var tags = (result as OkObjectResult)!.Value as List<TagBaseDTO>;

        tags.Should().NotBeNull();
        tags!.Should().HaveCount(3);
        tags.Select(t => t.Tag_Id).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task GetTagById_ValidId_ReturnsTag()
    {
        // Act
        var result = await _controller.GetTagById(1);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;

        var value = okResult!.Value;
        value.Should().NotBeNull();

        dynamic tag = value!;
        ((int)tag.Tag_Id).Should().Be(1);
        ((string)tag.Tag_Name).Should().Be("Vegetarian");
    }

    [Fact]
    public async Task GetTagById_InvalidId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetTagById(999);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetTags_AllTagsHaveColors()
    {
        // Act
        var result = await _controller.GetTags();

        // Assert
        var tags = (result as OkObjectResult)!.Value as List<TagBaseDTO>;
        tags!.Should().OnlyContain(t => System.Text.RegularExpressions.Regex.IsMatch(t.Tag_Color, "^#[0-9A-Fa-f]{6}$"));
    }

    [Fact]
    public async Task GetTagById_DatabaseError_Returns500WithTheId()
    {
        // Arrange: a disposed context makes every query throw
        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("broken").Options);
        context.Dispose();
        var controller = new TagController(context, Mock.Of<ILogger<TagController>>());

        // Act
        var result = await controller.GetTagById(7);

        // Assert
        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(500);
        Detail(result).Should().Be("Error retrieving tag 7");
    }

    [Fact]
    public async Task CreateTag_NewName_ReturnsCreatedPointingAtGetTagById()
    {
        // Act
        var result = await _controller.CreateTag(new TagBaseDTO { Tag_Name = "Vegan", Tag_Color = "#A0E0A0" });

        // Assert
        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(TagController.GetTagById));
        _context.Tags.Should().Contain(t => t.tag_name == "Vegan");
    }

    [Fact]
    public async Task CreateTag_DuplicateName_ReturnsConflict()
    {
        // Act
        var result = await _controller.CreateTag(new TagBaseDTO { Tag_Name = "Spicy", Tag_Color = "#A0E0A0" });

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
        Detail(result).Should().Be("Tag already exists");
        _context.Tags.Count(t => t.tag_name == "Spicy").Should().Be(1);
    }

    [Fact]
    public async Task UpdateTag_NameUsedByAnotherTag_ReturnsConflictAndKeepsName()
    {
        // Act
        var result = await _controller.UpdateTag(1, new TagUpdateDTO { Tag_Name = "Spicy" });

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
        Detail(result).Should().Be("Tag already exists");
        (await _context.Tags.FindAsync(1))!.tag_name.Should().Be("Vegetarian");
    }

    [Fact]
    public async Task UpdateTag_KeepingItsOwnName_ReturnsOk()
    {
        // Act
        var result = await _controller.UpdateTag(2, new TagUpdateDTO { Tag_Name = "Spicy", Tag_Color = "#CC0000" });

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        (await _context.Tags.FindAsync(2))!.tag_color.Should().Be("#CC0000");
    }

    [Fact]
    public async Task UpdateTag_ColorOnly_DoesNotCheckTheName()
    {
        // Act
        var result = await _controller.UpdateTag(1, new TagUpdateDTO { Tag_Color = "#123456" });

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        (await _context.Tags.FindAsync(1))!.tag_name.Should().Be("Vegetarian");
    }

    [Fact]
    public async Task UpdateTag_InvalidId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.UpdateTag(999, new TagUpdateDTO { Tag_Name = "Anything" });

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}