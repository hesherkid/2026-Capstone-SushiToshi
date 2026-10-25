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
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _mockLogger = new Mock<ILogger<OrderController>>();
        _controller = new OrderController(_context, _mockLogger.Object);

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

        // A second table, so tests can try reaching an order at a table the caller isn't seated at
        var otherSession = new DiningSession
        {
            Session_Id = 2,
            Location_Id = 1,
        };

        var otherBill = new Billing
        {
            Bill_Id = 3,
            Session_Id = 2,
            Bill_Name = "Other Table Bill",
            Senior_Count = 0,
            Adult_Count = 1,
            Child_Count = 0,
            Total_Count = 1,
            Status = BillStatus.Open,
            Created_At = DateTime.UtcNow
        };

        _context.Locations.Add(location);
        _context.DiningSessions.AddRange(session, otherSession);
        _context.Bills.AddRange(bill, otherBill);
        _context.SessionParticipants.AddRange(
            new SessionParticipant { Session_Id = 1, User_Id = 123, User_Name = "User One" },
            new SessionParticipant { Session_Id = 1, User_Id = 456, User_Name = "User Two" },
            // Left the table, so no longer counts as seated
            new SessionParticipant { Session_Id = 1, User_Id = 789, User_Name = "Gone", Left_At = DateTime.UtcNow });
        _context.SaveChanges();
    }

    private void SetupUserClaims(string userid, string userName = "Test User", string role = "Customer")
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userid),
            new Claim(ClaimTypes.Name, userName),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    private SessionOrder SeedOrder(OrderStatus orderStatus, params OrderStatus[] itemStatuses) =>
        SeedOrderAt(1, 1, orderStatus, itemStatuses);

    private SessionOrder SeedOrderAt(int sessionId, int billId, OrderStatus orderStatus, params OrderStatus[] itemStatuses)
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
            session_id = sessionId,
            Bill_Id = billId,
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
            Session_Id = 999,
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
            Bill_Id = 999
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
            Status = BillStatus.Paid,
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

        // Act
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
        // Arrange
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

        // Assert
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

    [Theory]
    [InlineData(nameof(OrderController.CreateOrder))]
    [InlineData(nameof(OrderController.AddOrderItems))]
    [InlineData(nameof(OrderController.GetOrderById))]
    [InlineData(nameof(OrderController.GetOrdersForActiveSession))]
    public void OrderEndpoints_RequireSignIn(string methodName)
    {
        var method = typeof(OrderController).GetMethod(methodName)!;

        method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Should().ContainSingle();
        method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), true)
            .Should().BeEmpty();
    }

    [Fact]
    public void GetOrderByUser_IsRemoved()
    {
        typeof(OrderController).GetMethod("GetOrderByUser").Should().BeNull();
    }

    [Fact]
    public async Task CreateOrder_CustomerNotSeatedAtTable_ReturnsForbid()
    {
        // Arrange: customer 123 is seated at session 1, not session 2
        SetupUserClaims("123");

        // Act
        var result = await _controller.CreateOrder(new OrderCreateDTO { Session_Id = 2, Bill_Id = 3 });

        // Assert
        result.Result.Should().BeOfType<ForbidResult>();
        _context.SessionOrders.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateOrder_CustomerWhoLeftTheTable_ReturnsForbid()
    {
        // Arrange
        SetupUserClaims("789");

        // Act
        var result = await _controller.CreateOrder(new OrderCreateDTO { Session_Id = 1, Bill_Id = 1 });

        // Assert
        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task CreateOrder_CustomerSetsRequestBy_IsIgnored()
    {
        // Arrange
        SetupUserClaims("123", "User One");

        // Act
        var result = await _controller.CreateOrder(new OrderCreateDTO
        {
            Session_Id = 1,
            Bill_Id = 1,
            Request_By_User_Id = 456,
            Request_By_Name = "User Two"
        });

        // Assert
        var order = (result.Result as OkObjectResult)!.Value as OrderResponseDTO;
        order!.User_Id.Should().Be(123);
        order.User_Name.Should().Be("User One");
    }

    [Fact]
    public async Task CreateOrder_StaffOrderingForCustomer_UsesRequestBy()
    {
        // Arrange: staff aren't seated at the table but can order at any table
        SetupUserClaims("42", "Server", "Staff");

        // Act
        var result = await _controller.CreateOrder(new OrderCreateDTO
        {
            Session_Id = 1,
            Bill_Id = 1,
            Request_By_User_Id = 123,
            Request_By_Name = "User One"
        });

        // Assert
        var order = (result.Result as OkObjectResult)!.Value as OrderResponseDTO;
        order!.User_Id.Should().Be(123);
        order.User_Name.Should().Be("User One");
    }

    [Fact]
    public async Task AddOrderItems_CustomerNotSeatedAtTable_ReturnsForbid()
    {
        // Arrange
        var order = SeedOrderAt(2, 3, OrderStatus.Pending, OrderStatus.Pending);
        SetupUserClaims("123");

        // Act
        var result = await _controller.AddOrderItems(order.Order_Id,
            new List<OrderItemCreateDTO> { new() { Item_Id = 1, Quantity = 1 } });

        // Assert
        result.Should().BeOfType<ForbidResult>();
        var saved = await _context.SessionOrders.Include(o => o.OrderItems)
            .SingleAsync(o => o.Order_Id == order.Order_Id);
        saved.OrderItems.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetOrderById_CustomerSeatedAtTable_ReturnsOrder()
    {
        // Arrange
        var order = SeedOrder(OrderStatus.Pending, OrderStatus.Pending);
        SetupUserClaims("456");

        // Act
        var result = await _controller.GetOrderById(order.Order_Id);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetOrderById_CustomerNotSeatedAtTable_ReturnsForbid()
    {
        // Arrange
        var order = SeedOrderAt(2, 3, OrderStatus.Pending, OrderStatus.Pending);
        SetupUserClaims("123");

        // Act
        var result = await _controller.GetOrderById(order.Order_Id);

        // Assert
        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetOrderById_Staff_ReturnsAnyOrder()
    {
        // Arrange
        var order = SeedOrderAt(2, 3, OrderStatus.Pending, OrderStatus.Pending);
        SetupUserClaims("42", "Server", "Staff");

        // Act
        var result = await _controller.GetOrderById(order.Order_Id);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }
}
