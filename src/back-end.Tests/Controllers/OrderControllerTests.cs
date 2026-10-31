using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using back_end.Controllers;
using back_end.Services;
using back_end.Helpers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.OrdersDTOs;
using System.Security.Claims;
using System.Text.Json;

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
        _controller = new OrderController(_context, _mockLogger.Object, new LocationAccessService(_context));

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
        // Staff only see sessions at the location they're working at
        _context.Users.Add(new User { User_id = 42, Email = "staff@example.com", Role = UserRoles.Staff, Location_id = 1 });
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
        SetupUserClaims("42", "Server", "Staff");
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
        SetupUserClaims("42", "Server", "Staff");
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
        SetupUserClaims("42", "Server", "Staff");
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
        SetupUserClaims("42", "Server", "Staff");
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
        SetupUserClaims("42", "Server", "Staff");
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
        SetupUserClaims("42", "Server", "Staff");
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

    private const int OpenSessionId = 10;
    private const int EndedSessionId = 11;
    private const int OtherLocationSessionId = 12;

    private void SeedAllOrderSessions()
    {
        _context.DiningSessions.AddRange(
            new DiningSession { Session_Id = OpenSessionId, Menu_Id = 1, Location_Id = 1, Started_At = DateTime.UtcNow.AddHours(-1) },
            new DiningSession { Session_Id = EndedSessionId, Menu_Id = 1, Location_Id = 1, Started_At = DateTime.UtcNow.AddHours(-3), Ended_At = DateTime.UtcNow.AddHours(-2) },
            new DiningSession { Session_Id = OtherLocationSessionId, Menu_Id = 1, Location_Id = 2, Started_At = DateTime.UtcNow.AddHours(-1) });

        _context.Bills.AddRange(
            new Billing { Bill_Id = OpenSessionId, Session_Id = OpenSessionId, Bill_Name = "Open", Status = BillStatus.Open, Created_At = DateTime.UtcNow },
            new Billing { Bill_Id = EndedSessionId, Session_Id = EndedSessionId, Bill_Name = "Ended", Status = BillStatus.Closed, Created_At = DateTime.UtcNow },
            new Billing { Bill_Id = OtherLocationSessionId, Session_Id = OtherLocationSessionId, Bill_Name = "Other", Status = BillStatus.Open, Created_At = DateTime.UtcNow });

        _context.SaveChanges();
    }

    private static int[] OrderIds(ActionResult<IEnumerable<object>> result)
    {
        result.Result.Should().BeOfType<OkObjectResult>();
        var json = JsonSerializer.SerializeToElement((result.Result as OkObjectResult)!.Value);
        return json.EnumerateArray().Select(o => o.GetProperty("orderId").GetInt32()).ToArray();
    }

    [Fact]
    public async Task GetAllOrdersByLocation_FlagOn_ExcludedDeliveredOrdersFromEndedSessions()
    {
        // Arrange
        SeedAllOrderSessions();
        SetupUserClaims("1", role: "Admin");
        var openDelivered = SeedOrderAt(OpenSessionId, OpenSessionId, OrderStatus.Delivered, OrderStatus.Delivered);
        var endedDelivered = SeedOrderAt(EndedSessionId, EndedSessionId, OrderStatus.Delivered, OrderStatus.Delivered);

        // Act
        var ids = OrderIds(await _controller.GetAllOrdersByLocation(locationId: 1, deliveredOpenSessionsOnly: true));

        // Assert
        ids.Should().Contain(openDelivered.Order_Id);
        ids.Should().NotContain(endedDelivered.Order_Id);
    }

    [Fact]
    public async Task GelAllOrdersByLocation_FlagOn_KeepsPendingAndProcessingRegardlessOfSession()
    {
        // Arrange
        SeedAllOrderSessions();
        SetupUserClaims("1", role: "Admin");
        var openPending = SeedOrderAt(OpenSessionId, OpenSessionId, OrderStatus.Pending, OrderStatus.Pending);
        var endedProcessing = SeedOrderAt(EndedSessionId, EndedSessionId, OrderStatus.Processing, OrderStatus.Processing);

        // Act
        var ids = OrderIds(await _controller.GetAllOrdersByLocation(locationId: 1, deliveredOpenSessionsOnly: true));

        // Assert
        ids.Should().Contain(openPending.Order_Id);
        ids.Should().Contain(endedProcessing.Order_Id);
    }

    [Fact]
    public async Task GetAllOrdersByLocation_FlagOff_ByDefault_ReturnsAllOrders()
    {
        // Arrange
        SeedAllOrderSessions();
        SetupUserClaims("1", role: "Admin");
        var openDelivered = SeedOrderAt(OpenSessionId, OpenSessionId, OrderStatus.Delivered, OrderStatus.Delivered);
        var openPending = SeedOrderAt(OpenSessionId, OpenSessionId, OrderStatus.Pending, OrderStatus.Pending);
        var endedDelivered = SeedOrderAt(EndedSessionId, EndedSessionId, OrderStatus.Delivered, OrderStatus.Delivered);
        var endedProcessing = SeedOrderAt(EndedSessionId, EndedSessionId, OrderStatus.Processing, OrderStatus.Processing);

        // Act
        var ids = OrderIds(await _controller.GetAllOrdersByLocation(locationId: 1));

        // Assert
        ids.Should().BeEquivalentTo(new[]
        {
            openDelivered.Order_Id, openPending.Order_Id,
            endedDelivered.Order_Id, endedProcessing.Order_Id
        });

    }

    [Fact]
    public async Task GetAllOrdersByLocation_FlagOn_StillRespectsLocationFilter()
    {
        // Arrange
        SeedAllOrderSessions();
        SetupUserClaims("1", role: "Admin");
        SeedOrderAt(OpenSessionId, OpenSessionId, OrderStatus.Delivered, OrderStatus.Delivered);
        var otherLocation = SeedOrderAt(OtherLocationSessionId, OtherLocationSessionId, OrderStatus.Delivered, OrderStatus.Delivered);

        // Act
        var ids = OrderIds(await _controller.GetAllOrdersByLocation(locationId: 2, deliveredOpenSessionsOnly: true));

        // Assert
        ids.Should().Equal(otherLocation.Order_Id);
    }

    private static readonly TimeZoneInfo RestaurantZone = AnalyticsDateRange.RestaurantTimeZone;

    private static DateOnly LocalToday =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, RestaurantZone));

    private static DateTime LocalToUtc(DateOnly day, int hour, int minute = 0) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(new TimeOnly(hour, minute)), RestaurantZone);

    private static string Day(DateOnly day) => day.ToString("yyyy-MM-dd");

    private SessionOrder SeedHistoryOrder(int sessionId, DateTime createdAtUtc,
        OrderStatus status = OrderStatus.Delivered, string? userName = null)
    {
        var order = SeedOrderAt(sessionId, sessionId, status, status);
        var saved = _context.SessionOrders.Single(o => o.Order_Id == order.Order_Id);
        saved.Created_At = createdAtUtc;
        saved.User_Name = userName;
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
        return saved;
    }

    private void SeedHistoryTables()
    {
        _context.TableGroups.Add(new TableGroup { TableGroup_Id = 1, Group_Name = "Booth", Location_Id = 1 });
        _context.Tables.AddRange(
            new TableEntity { Table_Id = 501, table_number = 14, Location_Id = 1 },
            new TableEntity { Table_Id = 502, table_number = 1, Location_Id = 1, TableGroup_Id = 1 },
            new TableEntity { Table_Id = 503, table_number = 2, Location_Id = 1, TableGroup_Id = 1 });
        _context.DiningSessions.Find(EndedSessionId)!.Table_Id = 501;
        _context.DiningSessions.Find(OpenSessionId)!.TableGroup_Id = 1;
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    private static ValidationProblemDetails BadRequestDetails(ActionResult<IEnumerable<object>> result)
    {
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        return (result.Result as BadRequestObjectResult)!.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
    }

    [Fact]
    public async Task GetOrderHistory_ReturnsOnlyOrdersInsideTheRange()
    {
        // Arrange
        SeedAllOrderSessions();
        SetupUserClaims("1", role: "Admin");
        var yesterday = LocalToday.AddDays(-1);
        var inRange = SeedHistoryOrder(EndedSessionId, LocalToUtc(yesterday, 12));
        var tooOld = SeedHistoryOrder(EndedSessionId, LocalToUtc(yesterday.AddDays(-4), 12));

        // Act
        var ids = OrderIds(await _controller.GetOrderHistory(Day(yesterday), Day(yesterday), locationId: 1));

        // Assert
        ids.Should().Equal(inRange.Order_Id);
        ids.Should().NotContain(tooOld.Order_Id);
    }

    [Fact]
    public async Task GetOrderHistory_ReturnsEveryStatus_FromOpenAndEndedSessions()
    {
        // Arrange
        SeedAllOrderSessions();
        SetupUserClaims("1", role: "Admin");
        var day = LocalToday.AddDays(-1);
        var delivered = SeedHistoryOrder(EndedSessionId, LocalToUtc(day, 12), OrderStatus.Delivered);
        var cancelled = SeedHistoryOrder(EndedSessionId, LocalToUtc(day, 13), OrderStatus.Cancelled);
        var pending = SeedHistoryOrder(OpenSessionId, LocalToUtc(day, 14), OrderStatus.Pending);

        // Act
        var ids = OrderIds(await _controller.GetOrderHistory(Day(day), Day(day), locationId: 1));

        // Assert
        ids.Should().BeEquivalentTo(new[] { delivered.Order_Id, cancelled.Order_Id, pending.Order_Id });
    }

    [Fact]
    public async Task GetOrderHistory_TableNumber_MatchesSingleTablesAndTableGroups()
    {
        // Arrange
        SeedAllOrderSessions();
        SeedHistoryTables();
        SetupUserClaims("1", role: "Admin");
        var day = LocalToday.AddDays(-1);
        var table14 = SeedHistoryOrder(EndedSessionId, LocalToUtc(day, 12));
        var tables1And2 = SeedHistoryOrder(OpenSessionId, LocalToUtc(day, 13));

        // Act
        var single = OrderIds(await _controller.GetOrderHistory(Day(day), Day(day), locationId: 1, tableNumber: 14));
        var grouped = OrderIds(await _controller.GetOrderHistory(Day(day), Day(day), locationId: 1, tableNumber: 2));

        // Assert
        single.Should().Equal(table14.Order_Id);
        grouped.Should().Equal(tables1And2.Order_Id);
    }

    [Fact]
    public async Task GetOrderHistory_Name_MatchesBillOrGuestName_CaseInsensitive()
    {
        // Arrange
        SeedAllOrderSessions();
        SetupUserClaims("1", role: "Admin");
        var day = LocalToday.AddDays(-1);
        var byBill = SeedHistoryOrder(EndedSessionId, LocalToUtc(day, 12));
        var byGuest = SeedHistoryOrder(OpenSessionId, LocalToUtc(day, 13), userName: "Lee Park");
        var noMatch = SeedHistoryOrder(OpenSessionId, LocalToUtc(day, 14), userName: "Sam");

        // Act
        var billIds = OrderIds(await _controller.GetOrderHistory(Day(day), Day(day), locationId: 1, name: "ENDE"));
        var guestIds = OrderIds(await _controller.GetOrderHistory(Day(day), Day(day), locationId: 1, name: " lee "));

        // Assert
        billIds.Should().Equal(byBill.Order_Id);
        guestIds.Should().Equal(byGuest.Order_Id);
        guestIds.Should().NotContain(noMatch.Order_Id);
    }

    [Fact]
    public async Task GetOrderHistory_ExactlyMaxRangeDays_IsAllowed()
    {
        // Arrange
        SetupUserClaims("1", role: "Admin");
        var from = LocalToday.AddDays(-(OrderController.MaxHistoryRangeDays - 1));

        // Act
        var result = await _controller.GetOrderHistory(Day(from), Day(LocalToday), locationId: 1);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetOrderHistory_RangeOverMaxDays_ReturnsBadRequest()
    {
        // Arrange
        SetupUserClaims("1", role: "Admin");
        var from = LocalToday.AddDays(-OrderController.MaxHistoryRangeDays);

        // Act
        var result = await _controller.GetOrderHistory(Day(from), Day(LocalToday), locationId: 1);

        // Assert
        BadRequestDetails(result).Errors.Should().ContainKey("to");
    }

    [Theory]
    [InlineData(null, "2026-01-01", "from")]
    [InlineData("2026-01-01", null, "to")]
    [InlineData("01/01/2026", "2026-01-02", "from")]
    [InlineData("2026-01-05", "2026-01-01", "from")]
    public async Task GetOrderHistory_InvalidDates_ReturnBadRequestKeyedByParameter(string? from, string? to, string expectedKey)
    {
        // Arrange
        SetupUserClaims("1", role: "Admin");

        // Act
        var result = await _controller.GetOrderHistory(from, to, locationId: 1);

        // Assert
        BadRequestDetails(result).Errors.Should().ContainKey(expectedKey);
    }

    [Fact]
    public async Task GetOrderHistory_FutureEndDate_ReturnsBadRequest()
    {
        // Arrange
        SetupUserClaims("1", role: "Admin");

        // Act
        var result = await _controller.GetOrderHistory(Day(LocalToday), Day(LocalToday.AddDays(1)), locationId: 1);

        // Assert
        BadRequestDetails(result).Errors.Should().ContainKey("to");
    }

    private SessionOrder SeedOrderOwnedBy(int? ownerId, OrderStatus orderStatus, params OrderStatus[] itemStatuses)
    {
        var order = SeedOrder(orderStatus, itemStatuses);

        var tracked = _context.SessionOrders.Single(o => o.Order_Id == order.Order_Id);
        tracked.User_Id = ownerId;
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        order.User_Id = ownerId;
        return order;
    }

    private bool ItemExists(int orderItemId) =>
        _context.OrderItems.AsNoTracking().Any(oi => oi.Order_Item_Id == orderItemId);


    [Fact]
    public async Task RemoveOrderItem_PendingOrder_DeletesOnlyThatItem()
    {
        SetupUserClaims("123");
        var order = SeedOrderOwnedBy(123, OrderStatus.Pending, OrderStatus.Pending, OrderStatus.Pending);
        var target = order.OrderItems.First().Order_Item_Id;
        var keep = order.OrderItems.Last().Order_Item_Id;

        var result = await _controller.RemoveOrderItem(order.Order_Id, target);

        result.Should().BeOfType<OkObjectResult>();
        ItemExists(target).Should().BeFalse();
        ItemExists(keep).Should().BeTrue();
    }

    [Fact]
    public async Task RemoveOrderItem_StaffApprovedOrder_DeletesItem()
    {
        SetupUserClaims("42", role: "Staff");
        var order = SeedOrderOwnedBy(123, OrderStatus.Processing, OrderStatus.Processing);
        var target = order.OrderItems.First().Order_Item_Id;

        var result = await _controller.RemoveOrderItem(order.Order_Id, target);

        result.Should().BeOfType<OkObjectResult>();
        ItemExists(target).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveOrderItem_NonOwnerCustomerApprovedOrder_ReturnsConflict()
    {
        SetupUserClaims("456");
        var order = SeedOrderOwnedBy(123, OrderStatus.Processing, OrderStatus.Processing);
        var target = order.OrderItems.First().Order_Item_Id;

        var result = await _controller.RemoveOrderItem(order.Order_Id, target);

        result.Should().BeOfType<ConflictObjectResult>();
        ItemExists(target).Should().BeTrue();
    }

    [Fact]
    public async Task RemoveOrderItem_OrderNotFound_ReturnsNotFound()
    {
        SetupUserClaims("123");

        var result = await _controller.RemoveOrderItem(999, 1);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RemoveOrderItem_ItemFromAnotherOrder_ReturnsBadRequest()
    {
        SetupUserClaims("123");
        var order1 = SeedOrderOwnedBy(123, OrderStatus.Pending, OrderStatus.Pending);
        var order2 = SeedOrderOwnedBy(123, OrderStatus.Pending, OrderStatus.Pending);
        var target = order2.OrderItems.First().Order_Item_Id;

        var result = await _controller.RemoveOrderItem(order1.Order_Id, target);

        result.Should().BeOfType<BadRequestObjectResult>();
        ItemExists(target).Should().BeTrue();
    }

    [Fact]
    public async Task RemoveOrderItem_CustomerNotSeatedAtTable_ReturnsForbidden()
    {
        SetupUserClaims("123");
        var order = SeedOrderAt(2, 3, OrderStatus.Pending, OrderStatus.Pending);
        var target = order.OrderItems.First().Order_Item_Id;

        var result = await _controller.RemoveOrderItem(order.Order_Id, target);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        ItemExists(target).Should().BeTrue();
    }
}