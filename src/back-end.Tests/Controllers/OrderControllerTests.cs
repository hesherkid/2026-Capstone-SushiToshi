using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.OrdersDTOs;
using System.Security.Claims;

namespace back_end.Tests.Controllers;

public class OrderControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<OrderController>> _mockLogger;
    private readonly OrderController _controller;

    public OrderControllerTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _mockLogger = new Mock<ILogger<OrderController>>();
        _controller = new OrderController(_context, _mockLogger.Object);

        // Setup test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        var location = new Locations
        {
            Location_Id = 1,
            Name = "Test Location",
            Address_Primary = "123 Test St",
            City = "Test City",
            Province = "TC",
            Postal_Code = "T1T 1T1",
            Phone_Number = "123-456-7890"
        };

        var session = new DiningSession
        {
            Session_Id = 1,
            Location_Id = 1,
        };

        var bill = new Billing
        {
            Bill_Id = 1,
            Session_Id = 1,
            Bill_Name = "Test Bill",
            Senior_Count = 0,
            Adult_Count = 1,
            Child_Count = 0,
            Total_Count = 1,
            Status = BillStatus.Open,
            Created_At = DateTime.UtcNow
        };

        _context.Locations.Add(location);
        _context.DiningSessions.Add(session);
        _context.Bills.Add(bill);
        _context.SaveChanges();
    }

    private void SetupUserClaims(string userid, string userName = "Test User")
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userid),
            new Claim(ClaimTypes.Name, userName),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, "Customer")
        };

        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    private SessionOrder SeedOrder(OrderStatus orderStatus, params OrderStatus[] itemStatuses)
    {
        if (!_context.MenuItems.Any())
        {
            _context.Menus.Add(new Menu
            {
                Menu_id = 1,
                Name = "Dinner",
                Description = "Dinner",
                Start_time = new TimeOnly(16, 0),
                End_time = new TimeOnly(22, 0),
                Is_active = true
            });
            _context.Categories.Add(new Category { Category_id = 1, Category_name = "Nigiri", Description = "Nigiri" });
            _context.MenuItems.Add(new Menu_Item
            {
                item_id = 1,
                Name = "Salmon Nigiri",
                Description = "Salmon",
                Category_id = 1,
                Status = MenuItemStatus.Available
            });
        }

        var order = new SessionOrder
        {
            session_id = 1,
            Bill_Id = 1,
            Status = orderStatus,
            Created_At = DateTime.UtcNow.AddMinutes(-10),
            Approved_At = orderStatus == OrderStatus.Pending ? null : DateTime.UtcNow.AddMinutes(-8),
            OrderItems = itemStatuses.Select(s => new OrderItems
            {
                Menu_Id = 1,
                Item_Id = 1,
                Quantity = 1,
                Price_At_Time = 0m,
                Order_Item_Status = s
            }).ToList()
        };

        _context.SessionOrders.Add(order);
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
        return order;
    }

    [Fact]
    public async Task CreateOrder_ValidData_CreatesOrder()
    {
        // Arrange
        SetupUserClaims("123", "Test User");

        var orderCreateDto = new OrderCreateDTO
        {
            Session_Id = 1,
            Bill_Id = 1
        };

        // Act
        var result = await _controller.CreateOrder(orderCreateDto);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var orderResponse = okResult!.Value as OrderResponseDTO;

        orderResponse.Should().NotBeNull();
        orderResponse!.Session_Id.Should().Be(1);
        orderResponse.Bill_Id.Should().Be(1);
        orderResponse.User_Id.Should().Be(123);
        orderResponse.User_Name.Should().Be("Test User");
        orderResponse.Status.Should().Be(OrderStatus.Pending);

        // Verify order was saved to database
        var savedOrder = await _context.SessionOrders.FirstOrDefaultAsync();
        savedOrder.Should().NotBeNull();
        savedOrder!.User_Id.Should().Be(123);
    }

    [Fact]
    public async Task CreateOrder_InvalidSession_ReturnsNotFound()
    {
        // Arrange
        SetupUserClaims("123");

        var orderCreateDto = new OrderCreateDTO
        {
            Session_Id = 999, // Non-existent session
            Bill_Id = 1
        };

        // Act
        var result = await _controller.CreateOrder(orderCreateDto);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateOrder_InvalidBill_ReturnsNotFound()
    {
        // Arrange
        SetupUserClaims("123");

        var orderCreateDto = new OrderCreateDTO
        {
            Session_Id = 1,
            Bill_Id = 999 // Non-existent bill
        };

        // Act
        var result = await _controller.CreateOrder(orderCreateDto);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateOrder_ClosedBill_ReturnsNotFound()
    {
        // Arrange
        SetupUserClaims("123");

        var closedBill = new Billing
        {
            Bill_Id = 2,
            Session_Id = 1,
            Bill_Name = "Closed Bill",
            Senior_Count = 0,
            Adult_Count = 2,
            Child_Count = 0,
            Total_Count = 2,
            Status = BillStatus.Paid, // Closed bill
            Created_At = DateTime.UtcNow
        };

        _context.Bills.Add(closedBill);
        await _context.SaveChangesAsync();

        var orderCreateDto = new OrderCreateDTO
        {
            Session_Id = 1,
            Bill_Id = 2
        };

        // Act
        var result = await _controller.CreateOrder(orderCreateDto);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateOrder_SetsCorrectTimestamp()
    {
        // Arrange
        SetupUserClaims("123");
        var beforeCreation = DateTime.UtcNow;

        var orderCreateDto = new OrderCreateDTO
        {
            Session_Id = 1,
            Bill_Id = 1
        };

        // Act
        await _controller.CreateOrder(orderCreateDto);

        // Assert
        var savedOrder = await _context.SessionOrders.FirstOrDefaultAsync();
        savedOrder.Should().NotBeNull();
        savedOrder!.Created_At.Should().BeAfter(beforeCreation);
        savedOrder.Created_At.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(123, "User One")]
    [InlineData(456, "User Two")]
    public async Task CreateOrder_DifferentUsers_CreatesOrdersWithCorrectUserId(int userId, string userName)
    {
        // Arrange
        SetupUserClaims(userId.ToString(), userName);

        var orderCreateDto = new OrderCreateDTO
        {
            Session_Id = 1,
            Bill_Id = 1
        };

        // Act
        var result = await _controller.CreateOrder(orderCreateDto);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var orderResponse = okResult!.Value as OrderResponseDTO;

        orderResponse.Should().NotBeNull();
        orderResponse!.User_Id.Should().Be(userId);
        orderResponse.User_Name.Should().Be(userName);
    }

    [Fact]
    public async Task CreateOrder_MultipleOrders_AllCreatedSuccessfully()
    {
        // Arrange
        SetupUserClaims("123");

        // Act - Create 3 orders
        for (int i = 0; i < 3; i++)
        {
            var orderCreateDto = new OrderCreateDTO
            {
                Session_Id = 1,
                Bill_Id = 1
            };
            await _controller.CreateOrder(orderCreateDto);
        }

        // Assert
        var orders = await _context.SessionOrders.ToListAsync();
        orders.Should().HaveCount(3);
        orders.Should().AllSatisfy(o => o.Status.Should().Be(OrderStatus.Pending));
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task CreateOrder_WithRealLoginClaims_StoresEmailAsUserName()
    {
        // Arrange - mirrors AuthController.cs lines 309–312 (Name = Email, no given/family name)
        var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, "123"),
        new Claim(ClaimTypes.Name, "customer@example.com"),
        new Claim(ClaimTypes.Email, "customer@example.com"),
        new Claim(ClaimTypes.Role, "Customer")
    };
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthType"))
            }
        };

        // Act
        var result = await _controller.CreateOrder(new OrderCreateDTO { Session_Id = 1, Bill_Id = 1 });

        // Assert - current known behavior; update if real names are added to the token see TODO on Auth controller
        var order = (result.Result as OkObjectResult)!.Value as OrderResponseDTO;
        order!.User_Name.Should().Be("customer@example.com");
    }

    [Fact]
    public async Task CompleteOrderItem_DeliversOnlyThatItem_OrderStaysProcessing()
    {
        // Arrange
        var order = SeedOrder(OrderStatus.Processing, OrderStatus.Processing, OrderStatus.Processing);
        var itemId = order.OrderItems.First().Order_Item_Id;

        // Act
        var result = await _controller.CompleteOrderItem(order.Order_Id, itemId);

        // Assert
        var dto = result.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<OrderItemResponseDTO>().Subject;
        dto.Status.Should().Be(OrderStatus.Delivered);
        dto.Completed_At.Should().NotBeNull();

        var saved = await _context.SessionOrders.Include(o => o.OrderItems)
            .SingleAsync(o => o.Order_Id == order.Order_Id);
        saved.Status.Should().Be(OrderStatus.Processing);
        saved.Completed_At.Should().BeNull();
        saved.OrderItems.Count(i => i.Order_Item_Status == OrderStatus.Processing).Should().Be(1);
    }

    [Fact]
    public async Task CompleteOrderItem_LastOutstandingItem_CompletesOrder()
    {
        // Arrange
        var order = SeedOrder(OrderStatus.Processing,
            OrderStatus.Delivered, OrderStatus.Processing, OrderStatus.Cancelled);
        var lastId = order.OrderItems.Single(i => i.Order_Item_Status == OrderStatus.Processing).Order_Item_Id;

        // Act
        await _controller.CompleteOrderItem(order.Order_Id, lastId);

        // Assert
        var saved = await _context.SessionOrders.Include(o => o.OrderItems)
            .SingleAsync(o => o.Order_Id == order.Order_Id);
        var lastItem = saved.OrderItems.Single(i => i.Order_Item_Id == lastId);
        saved.Status.Should().Be(OrderStatus.Delivered);
        saved.Completed_At.Should().Be(lastItem.Completed_At);
    }

    [Fact]
    public async Task CompleteOrderItem_PendingItem_ReturnsBadRequest()
    {
        // Arrange
        var order = SeedOrder(OrderStatus.Pending, OrderStatus.Pending);

        // Act
        var result = await _controller.CompleteOrderItem(order.Order_Id, order.OrderItems.First().Order_Item_Id);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CompleteOrderItem_ItemFromAnotherOrder_ReturnsNotFound()
    {
        // Arrange
        var orderA = SeedOrder(OrderStatus.Processing, OrderStatus.Processing);
        var orderB = SeedOrder(OrderStatus.Processing, OrderStatus.Processing);

        // Act
        var result = await _controller.CompleteOrderItem(orderA.Order_Id, orderB.OrderItems.First().Order_Item_Id);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CompleteOrder_PendingOrder_ReturnsBadRequest()
    {
        // Arrange
        var order = SeedOrder(OrderStatus.Pending, OrderStatus.Pending);

        // Act
        var result = await _controller.CompleteOrder(order.Order_Id);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateOrder_SetsFirstOrderAt_OnlyOnFirstOrder()
    {
        // Arrange
        SetupUserClaims("123");

        // Act
        await _controller.CreateOrder(new OrderCreateDTO { Session_Id = 1, Bill_Id = 1 });
        var first = (await _context.DiningSessions.SingleAsync(s => s.Session_Id == 1)).First_Order_At;

        await _controller.CreateOrder(new OrderCreateDTO { Session_Id = 1, Bill_Id = 1 });
        var after = (await _context.DiningSessions.SingleAsync(s => s.Session_Id == 1)).First_Order_At;

        // Assert
        first.Should().NotBeNull();
        after.Should().Be(first);
    }

    [Fact]
    public async Task ApproveOrder_SetsApprovedAtAndApprover()
    {
        // Arrange
        SetupUserClaims("42");
        var order = SeedOrder(OrderStatus.Pending, OrderStatus.Pending);

        // Act
        var result = await _controller.ApproveOrder(order.Order_Id);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var saved = await _context.SessionOrders.SingleAsync(o => o.Order_Id == order.Order_Id);
        saved.Status.Should().Be(OrderStatus.Processing);
        saved.Approved_At.Should().NotBeNull();
        saved.Approved_At!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        saved.Approved_By.Should().Be(42);
    }
}
