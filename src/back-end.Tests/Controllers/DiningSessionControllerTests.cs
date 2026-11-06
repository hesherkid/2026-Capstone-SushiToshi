using System.Security.Claims;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using back_end.Controllers;
using back_end.controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.DiningSessionDTOs;
using back_end.DTO.TableGroupDTOs;
using back_end.Services;

namespace back_end.Tests.Controllers;

public class DiningSessionControllerTests : IDisposable
{
    private const int LocationId = 1;
    private const int StaffId = 1;
    private const int CustomerId = 4;
    private const int OtherCustomerId = 5;
    private const int Table1 = 11;
    private const int Table2 = 12;
    private const int GroupTableA = 13;
    private const int GroupTableB = 14;
    private const int GroupId = 50;
    private const int SessionId = 100;
    private const int OtherSessionId = 200;
    private const int BillId = 1;
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<DashBoardController>> _logger = new();
    private int _nextOrderItemId = 1;

    public DiningSessionControllerTests()
    {
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        _context.Locations.Add(new Locations { Location_Id = LocationId, Name = "Downtown" });
        _context.Menus.Add(new Menu { Menu_id = 1, Name = "Dinner" });
        _context.Users.AddRange(
            new User { User_id = StaffId, Email = "staff@example.com", Role = UserRoles.Staff, Location_id = LocationId },
            new User { User_id = CustomerId, Email = "emma@example.com", First_name = "Emma", Role = UserRoles.Customer },
            new User { User_id = OtherCustomerId, Email = "mia@example.com", First_name = "Mia", Role = UserRoles.Customer });
        _context.TableGroups.Add(new TableGroup { TableGroup_Id = GroupId, Group_Name = "Patio", Location_Id = LocationId, Is_Active = true });
        _context.Tables.AddRange(
            new TableEntity { Table_Id = Table1, table_number = 1, seat_count = 4, Location_Id = LocationId, is_active = true },
            new TableEntity { Table_Id = Table2, table_number = 2, seat_count = 4, Location_Id = LocationId, is_active = true },
            new TableEntity { Table_Id = GroupTableA, table_number = 3, seat_count = 4, Location_Id = LocationId, is_active = true, TableGroup_Id = GroupId },
            new TableEntity { Table_Id = GroupTableB, table_number = 4, seat_count = 4, Location_Id = LocationId, is_active = true, TableGroup_Id = GroupId });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private DiningSessionController Controller() =>
        new(_context, new Mock<IConfiguration>().Object, _logger.Object, new LocationAccessService(_context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, StaffId.ToString()),
                        new Claim(ClaimTypes.Role, UserRoles.Staff.ToString()),
                    }, "TestAuth")),
                },
            },
        };

    private DiningSession AddSession(int id = SessionId, int? tableId = Table1, int? groupId = null, bool ended = false)
    {
        var session = new DiningSession
        {
            Session_Id = id,
            Menu_Id = 1,
            Location_Id = LocationId,
            Table_Id = tableId,
            TableGroup_Id = groupId,
            Started_At = DateTime.UtcNow.AddHours(-1),
            Ended_At = ended ? DateTime.UtcNow.AddMinutes(-5) : null,
        };
        _context.DiningSessions.Add(session);
        _context.SaveChanges();
        return session;
    }

    private void AddBill(int billId, int sessionId, BillStatus status)
    {
        _context.Bills.Add(new Billing { Bill_Id = billId, Session_Id = sessionId, Bill_Name = "Bill", Status = status });
        _context.SaveChanges();
    }

    private void AddOrder(int orderId, int sessionId, OrderStatus orderStatus, params OrderStatus[] itemStatuses)
    {
        _context.SessionOrders.Add(new SessionOrder
        {
            Order_Id = orderId,
            session_id = sessionId,
            Bill_Id = BillId,
            User_Id = CustomerId,
            User_Name = "Emma",
            Status = orderStatus,
            Created_At = DateTime.UtcNow,
        });
        foreach (var itemStatus in itemStatuses)
        {
            _context.OrderItems.Add(new OrderItems
            {
                Order_Item_Id = _nextOrderItemId++,
                Order_Key = orderId,
                Menu_Id = 1,
                Item_Id = 1,
                Quantity = 1,
                Order_Item_Status = itemStatus,
            });
        }
        _context.SaveChanges();
    }

    private SessionParticipant AddSeat(int sessionId, int userId, DateTime? leftAt = null)
    {
        var seat = new SessionParticipant
        {
            Session_Id = sessionId,
            User_Id = userId,
            User_Name = $"User {userId}",
            Joined_At = DateTime.UtcNow.AddMinutes(-30),
            Left_At = leftAt,
        };
        _context.SessionParticipants.Add(seat);
        _context.SaveChanges();
        return seat;
    }

    private ServiceRequest AddRequest(int requestId, int sessionId, int tableId, ServiceRequestStatus status)
    {
        var request = new ServiceRequest
        {
            request_id = requestId,
            Session_Id = sessionId,
            Table_Id = tableId,
            Request_By = CustomerId,
            Notes = "Water",
            Status = status,
            Created_At = DateTime.UtcNow.AddMinutes(-10),
        };
        if (status == ServiceRequestStatus.Claimed)
        {
            request.Claimed_By = StaffId;
            request.Claimed_At = DateTime.UtcNow.AddMinutes(-8);
        }
        _context.ServiceRequests.Add(request);
        _context.SaveChanges();
        return request;
    }

    private static DiningSessionResponseDTO Body(IActionResult result) =>
        (DiningSessionResponseDTO)result.Should().BeOfType<OkObjectResult>().Subject.Value!;

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Processing)]
    public async Task Close_RefusesUnfinishItems(OrderStatus unfinished)
    {
        AddSession();
        AddBill(BillId, SessionId, BillStatus.Closed);
        AddOrder(1000, SessionId, OrderStatus.Processing, OrderStatus.Delivered, unfinished);

        var result = await Controller().CloseDiningSession(SessionId);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await _context.DiningSessions.FindAsync(SessionId))!.Ended_At.Should().BeNull();
    }

    [Fact]
    public async Task Close_AllowsDeliveredAndCancelledItems()
    {
        AddSession();
        AddBill(BillId, SessionId, BillStatus.Paid);
        AddOrder(1000, SessionId, OrderStatus.Delivered, OrderStatus.Delivered, OrderStatus.Cancelled);

        var result = await Controller().CloseDiningSession(SessionId);

        Body(result).Ended_at.Should().NotBeNull();
    }

    [Fact]
    public async Task Close_IgnoresItemsOnCancelledOrders()
    {
        AddSession();
        AddBill(BillId, SessionId, BillStatus.Cancelled);
        AddOrder(1000, SessionId, OrderStatus.Cancelled, OrderStatus.Pending);

        var result = await Controller().CloseDiningSession(SessionId);

        Body(result).Ended_at.Should().NotBeNull();
    }

    [Fact]
    public async Task Close_RefusesOpenBills()
    {
        AddSession();
        AddBill(BillId, SessionId, BillStatus.Open);

        var result = await Controller().CloseDiningSession(SessionId);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await _context.DiningSessions.FindAsync(SessionId))!.Ended_At.Should().BeNull();
    }

    [Fact]
    public async Task Close_RefusesAnAlreadyClosedSession()
    {
        AddSession(ended: true);

        var result = await Controller().CloseDiningSession(SessionId);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Close_CompletesPendingServiceRequests()
    {
        AddSession();
        AddSession(OtherSessionId, Table2);
        var pending = AddRequest(1, SessionId, Table1, ServiceRequestStatus.Pending);
        var claimed = AddRequest(2, SessionId, Table1, ServiceRequestStatus.Claimed);
        var otherTable = AddRequest(3, OtherSessionId, Table2, ServiceRequestStatus.Pending);
        var claimedAt = claimed.Claimed_At;

        var result = await Controller().CloseDiningSession(SessionId);

        var endedAt = Body(result).Ended_at;
        pending.Status.Should().Be(ServiceRequestStatus.Completed);
        pending.Completed_At.Should().Be(endedAt);
        pending.Claimed_At.Should().Be(endedAt);

        claimed.Status.Should().Be(ServiceRequestStatus.Claimed);
        claimed.Claimed_At.Should().Be(claimedAt);

        otherTable.Status.Should().Be(ServiceRequestStatus.Pending);
    }

    [Fact]
    public async Task Close_SeatsLeaveWithTheTable_AndEarlierLeavesKeepTheirTime()
    {
        AddSession();
        var seated = AddSeat(SessionId, CustomerId);
        var leftEarlier = DateTime.UtcNow.AddMinutes(-20);
        var alreadyLeft = AddSeat(SessionId, OtherCustomerId, leftEarlier);

        var result = await Controller().CloseDiningSession(SessionId);

        var body = Body(result);
        seated.Left_At.Should().Be(body.Ended_at);
        alreadyLeft.Left_At.Should().Be(leftEarlier);
        body.Active_Participants.Should().Be(0);
    }

    [Fact]
    public async Task RemoveTable_RefusesEndedSessions()
    {
        AddSession(ended: true);

        var result = await Controller().RemoveTableFromSession(SessionId, Table1);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await _context.DiningSessions.FindAsync(SessionId))!.Table_Id.Should().Be(Table1);
    }

    [Fact]
    public async Task RemoveTableGroup_RefusesEndedSessions()
    {
        AddSession(tableId: null, groupId: GroupId, ended: true);

        var result = await Controller().RemoveTableGroupFromSession(SessionId, GroupId);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await _context.DiningSessions.FindAsync(SessionId))!.TableGroup_Id.Should().Be(GroupId);
    }

    [Fact]
    public async Task RemoveTable_WorksOnActiveSession()
    {
        AddSession();

        var result = await Controller().RemoveTableFromSession(SessionId, Table1);

        result.Should().BeOfType<OkObjectResult>();
        (await _context.DiningSessions.FindAsync(SessionId))!.Table_Id.Should().BeNull();
    }

    [Fact]
    public async Task RemoveTableGroup_WorksOnActiveSession()
    {
        AddSession(tableId: null, groupId: GroupId);

        var result = await Controller().RemoveTableGroupFromSession(SessionId, GroupId);

        result.Should().BeOfType<OkObjectResult>();
        (await _context.DiningSessions.FindAsync(SessionId))!.TableGroup_Id.Should().BeNull();
    }

    [Fact]
    public async Task CountsOnlyGuestsStillSeated()
    {
        AddSession();
        AddSeat(SessionId, CustomerId);
        AddSeat(SessionId, OtherCustomerId, DateTime.UtcNow.AddMinutes(-5));

        var result = await Controller().ListDiningSessions(new ListDiningSessionsRequestDTO { ActiveOnly = true });

        var sessions = (IEnumerable<DiningSessionResponseDTO>)result.Should().BeOfType<OkObjectResult>().Subject.Value!;
        sessions.Single().Active_Participants.Should().Be(1);
    }

    [Fact]
    public async Task AddTable_CountsOnlyGuestsStillSeated()
    {
        AddSession(tableId: null);
        AddSeat(SessionId, CustomerId);
        AddSeat(SessionId, OtherCustomerId, DateTime.UtcNow.AddMinutes(-5));

        var result = await Controller().AddTableToDiningSession(SessionId, new TableAssignmentDTO { Table_Id = Table2 });

        Body(result).Active_Participants.Should().Be(1);
    }

    [Fact]
    public async Task AddTableGroup_CountsOnlyGuestsStillSeated()
    {
        AddSession(tableId: null);
        AddSeat(SessionId, CustomerId);
        AddSeat(SessionId, OtherCustomerId, DateTime.UtcNow.AddMinutes(-5));

        var result = await Controller().AddTableGroupToDiningSession(SessionId, new TableGroupAssignmentDTO { TableGroup_Id = GroupId });

        Body(result).Active_Participants.Should().Be(1);
    }

    [Fact]
    public async Task AddTable_RefusesATableAnotherSessionHas()
    {
        AddSession(SessionId, Table1);
        AddSession(OtherSessionId, tableId: null);

        var result = await Controller().AddTableToDiningSession(OtherSessionId, new TableAssignmentDTO { Table_Id = Table1 });

        result.Should().BeOfType<BadRequestObjectResult>();
        (await _context.DiningSessions.FindAsync(OtherSessionId))!.Table_Id.Should().BeNull();
    }

    [Fact]
    public async Task Errors_AreLoggedWithoutSendingExceptionDetails()
    {
        AddSession();
        var controller = Controller();
        _context.Dispose();

        var results = new IActionResult[]
        {
            await controller.CloseDiningSession(SessionId),
            await controller.GetDiningSession(SessionId),
            await controller.ListDiningSessions(new ListDiningSessionsRequestDTO { ActiveOnly = true }),
            await controller.RemoveTableFromSession(SessionId, Table1),
            await controller.RemoveTableGroupFromSession(SessionId, GroupId),
            await controller.AddTableToDiningSession(SessionId, new TableAssignmentDTO { Table_Id = Table2 }),
            await controller.AddTableGroupToDiningSession(SessionId, new TableGroupAssignmentDTO { TableGroup_Id = GroupId }),
        };

        foreach (var result in results)
        {
            var error = result.Should().BeOfType<ObjectResult>().Subject;
            error.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
            error.Value!.GetType().GetProperty("error").Should().BeNull();
        }

        _logger.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(results.Length));
    }
}