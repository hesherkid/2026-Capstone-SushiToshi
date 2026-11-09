using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using back_end.Configurations;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Dtos;
using back_end.Services;

namespace back_end.Tests.Controllers;

public class MenuAssignmentControllerTests : IDisposable
{
    private const int MainMenu = 1, Salmon = 10;

    private readonly ApplicationDbContext _context;
    private readonly MenuAssignmentController _controller;

    public MenuAssignmentControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _controller = new MenuAssignmentController(_context, Mock.Of<ILogger<MenuAssignmentController>>(), new LocationAccessService(_context))
        {
            // Admins may change the default row (no location)
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "1"),
                        new Claim(ClaimTypes.Role, nameof(UserRoles.Admin)),
                    }, "TestAuth")),
                },
            },
        };

        _context.Categories.Add(new Category { Category_id = 1, Category_name = "Nigiri", Description = "Nigiri" });
        _context.Menus.Add(new Menu { Menu_id = MainMenu, Name = "Main Menu", Description = "Main", Is_active = true });
        _context.MenuItems.Add(new Menu_Item { item_id = Salmon, Name = "Salmon", Description = "Salmon nigiri", Category_id = 1 });
        _context.SaveChanges();
    }

    [Fact]
    public async Task CreateMenuItemAssignment_ReturnsTheNewRow()
    {
        // Act
        var result = await _controller.CreateMenuItemAssignment(new MenuAssignmentCreate
        {
            MenuId = MainMenu,
            ItemId = Salmon,
            Price = 4m,
            AdultLimit = 2,
            ChildLimit = 1,
            SeniorLimit = 2,
            PerOrderLimit = 3,
            OverLimitBehavior = OverLimitBehavior.Charge,
        });

        // Assert
        var dto = result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<MenuAssignmentResponseDto>().Subject;
        dto.AssignmentId.Should().BePositive();
        dto.MenuId.Should().Be(MainMenu);
        dto.ItemId.Should().Be(Salmon);
        dto.LocationId.Should().BeNull();
        dto.AdultLimit.Should().Be(2);
        dto.PerOrderLimit.Should().Be(3);
        dto.OverLimitBehavior.Should().Be(OverLimitBehavior.Charge);
    }

    [Fact]
    public async Task UpdateMenuItemAssignment_ChangesOnlySentFieldsAndReturnsTheRow()
    {
        // Arrange
        _context.MenuItemAssignments.Add(new MenuItemAssignment { Menu_Id = MainMenu, Item_Id = Salmon, Price = 4m, Adult_Limit = 2, Child_limit = 1, Senior_limit = 2, Status = MenuItemStatus.Available });
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.UpdateMenuItemAssignment(MainMenu, Salmon, new MenuAssignmentUpdateDto { Price = 5m }, null);

        // Assert
        var dto = result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<MenuAssignmentResponseDto>().Subject;
        dto.Price.Should().Be(5m);
        dto.AdultLimit.Should().Be(2);
        dto.Status.Should().Be(MenuItemStatus.Available);
    }

    // In development the API rejects keys a DTO doesn't have, so the edit dialog
    // must leave out menu_id, item_id and location_id (they're in the URL instead)
    [Fact]
    public void UpdateDto_InDevelopment_AcceptsEditFieldsAndRejectsCreateOnlyKeys()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        ApiJsonOptions.Apply(options);

        var edit = JsonSerializer.Deserialize<MenuAssignmentUpdateDto>(
            """{"price": 5, "adult_limit": 2, "child_limit": 1, "senior_limit": 2, "tot_limit": 0, "per_order_limit": 0, "over_limit_behavior": "Block", "status": "available", "is_add_on": false}""",
            options);
        edit!.Price.Should().Be(5m);
        edit.Status.Should().Be(MenuItemStatus.Available);

        var withMenuId = () => JsonSerializer.Deserialize<MenuAssignmentUpdateDto>("""{"menu_id": 1, "price": 5}""", options);
        withMenuId.Should().Throw<JsonException>().WithMessage("*menu_id*");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
