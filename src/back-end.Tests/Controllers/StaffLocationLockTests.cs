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
using back_end.DTO.DashBoardDTOs;
using back_end.DTO.MenuItemAssignmentDTO;
using back_end.DTO.ServiceRequestDTOs;
using back_end.DTO.TableEntityDTOs;
using back_end.Services;

namespace back_end.Tests.Controllers;

// Staff only see and change data at the location they're working at, whatever location the
// browser asks for. Admins still work with every location
public class StaffLocationLockTests : IDisposable
{
    private const int Northside = 1;
    private const int Southside = 2;

    private const int StaffId = 1;           // working at Northside
    private const int UnassignedStaffId = 2; // no location yet
    private const int AdminId = 3;
    private const int CustomerId = 4;

    // Tables, sessions and orders at each location
    private const int NorthTable = 11;
    private const int SouthTable = 21;
    private const int NorthSession = 100;
    private const int SouthSession = 200;
    private const int NorthOrder = 1000;
    private const int SouthOrder = 2000;

    private readonly ApplicationDbContext _context;

    public StaffLocationLockTests()
    {
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        _context.Locations.AddRange(
            new Locations { Location_Id = Northside, Name = "Northside" },
            new Locations { Location_Id = Southside, Name = "Southside" });
        _context.Users.AddRange(
            new User { User_id = StaffId, Email = "staff@example.com", Role = UserRoles.Staff, Location_id = Northside },
            new User { User_id = UnassignedStaffId, Email = "new@example.com", Role = UserRoles.Staff },
            new User { User_id = AdminId, Email = "admin@example.com", Role = UserRoles.Admin, Location_id = Northside },
            new User { User_id = CustomerId, Email = "emma@example.com", Role = UserRoles.Customer });
        _context.Menus.Add(new Menu { Menu_id = 1, Name = "Dinner" });
        _context.Tables.AddRange(
            new TableEntity { Table_Id = NorthTable, table_number = 1, seat_count = 4, Location_Id = Northside, is_active = true },
            new TableEntity { Table_Id = SouthTable, table_number = 1, seat_count = 4, Location_Id = Southside, is_active = true });
        _context.DiningSessions.AddRange(
            new DiningSession { Session_Id = NorthSession, Menu_Id = 1, Location_Id = Northside, Table_Id = NorthTable, Started_At = DateTime.UtcNow },
            new DiningSession { Session_Id = SouthSession, Menu_Id = 1, Location_Id = Southside, Table_Id = SouthTable, Started_At = DateTime.UtcNow });
        _context.SessionOrders.AddRange(
            new SessionOrder { Order_Id = NorthOrder, session_id = NorthSession, User_Name = "Lee", Status = OrderStatus.Pending, Created_At = DateTime.UtcNow },
            new SessionOrder { Order_Id = SouthOrder, session_id = SouthSession, User_Name = "Park", Status = OrderStatus.Pending, Created_At = DateTime.UtcNow });
        _context.ServiceRequests.AddRange(
            new ServiceRequest { request_id = 1, Session_Id = NorthSession, Table_Id = NorthTable, Request_By = CustomerId, Notes = "Water", Status = ServiceRequestStatus.Pending },
            new ServiceRequest { request_id = 2, Session_Id = SouthSession, Table_Id = SouthTable, Request_By = CustomerId, Notes = "Napkins", Status = ServiceRequestStatus.Pending });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private static ControllerContext As(int userId, UserRoles role) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role.ToString()),
            }, "TestAuth")),
        },
    };

    private LocationAccessService LocationAccess() => new(_context);

    private TableEntityController Tables(int userId, UserRoles role) =>
        new(_context, new Mock<ILogger<TableEntityController>>().Object, LocationAccess()) { ControllerContext = As(userId, role) };

    private DiningSessionController Sessions(int userId, UserRoles role) =>
        new(_context, new Mock<IConfiguration>().Object, new Mock<ILogger<DashBoardController>>().Object, LocationAccess()) { ControllerContext = As(userId, role) };

    private OrderController Orders(int userId, UserRoles role) =>
        new(_context, new Mock<ILogger<OrderController>>().Object, LocationAccess()) { ControllerContext = As(userId, role) };

    private ServiceRequestController ServiceRequests(int userId, UserRoles role) =>
        new(_context, new Mock<ILogger<ServiceRequestController>>().Object, LocationAccess()) { ControllerContext = As(userId, role) };

    private DashBoardController Dashboard(int userId, UserRoles role) =>
        new(_context, new Mock<IConfiguration>().Object, new Mock<ILogger<DashBoardController>>().Object, LocationAccess()) { ControllerContext = As(userId, role) };

    private MenuAssignmentController MenuAssignments(int userId, UserRoles role) =>
        new(_context, new Mock<ILogger<MenuAssignmentController>>().Object, LocationAccess()) { ControllerContext = As(userId, role) };

    private static void ShouldBeForbidden(IActionResult? result) =>
        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

    // Lists: the location the browser sends is ignored for staff

    [Fact]
    public async Task Tables_GivesStaffTheirOwnLocation_WhenAnotherIsAskedFor()
    {
        var result = await Tables(StaffId, UserRoles.Staff).GetAllTables(locationId: Southside);

        var tables = (result.Result as OkObjectResult)!.Value as IEnumerable<TableEntity>;
        tables!.Select(t => t.Table_Id).Should().Equal(NorthTable);
    }

    [Fact]
    public async Task Tables_GivesStaffTheirOwnLocation_WhenNoneIsAskedFor()
    {
        var result = await Tables(StaffId, UserRoles.Staff).GetAllTables(locationId: null);

        var tables = (result.Result as OkObjectResult)!.Value as IEnumerable<TableEntity>;
        tables!.Select(t => t.Table_Id).Should().Equal(NorthTable);
    }

    [Fact]
    public async Task Tables_GivesAdminsTheLocationTheyAskFor()
    {
        var result = await Tables(AdminId, UserRoles.Admin).GetAllTables(locationId: Southside);

        var tables = (result.Result as OkObjectResult)!.Value as IEnumerable<TableEntity>;
        tables!.Select(t => t.Table_Id).Should().Equal(SouthTable);
    }

    [Fact]
    public async Task Tables_ForbidsStaffWithNoLocation()
    {
        var result = await Tables(UnassignedStaffId, UserRoles.Staff).GetAllTables(locationId: Northside);

        result.Result.Should().BeOfType<ObjectResult>()
            .Which.Value.Should().Be(ILocationAccessService.NoLocationMessage);
    }

    [Fact]
    public async Task Dashboard_GivesStaffTheirOwnLocation_WhenAnotherIsAskedFor()
    {
        var result = await Dashboard(StaffId, UserRoles.Staff).GetDashBoardSessions(locationId: Southside);

        var sessions = (result as OkObjectResult)!.Value as IEnumerable<DashBoardSessionsDTO>;
        sessions!.Select(s => s.Location_Id).Should().OnlyContain(id => id == Northside);
    }

    [Fact]
    public async Task PendingServiceRequests_ShowStaffOnlyTheirLocation()
    {
        var result = await ServiceRequests(StaffId, UserRoles.Staff).GetAllPendingServiceRequests();

        var requests = (result.Result as OkObjectResult)!.Value as IEnumerable<ServiceRequestResponseDTO>;
        requests!.Select(r => r.Session_Id).Should().Equal(NorthSession);
    }

    [Fact]
    public async Task PendingServiceRequests_ShowAdminsEveryLocation()
    {
        var result = await ServiceRequests(AdminId, UserRoles.Admin).GetAllPendingServiceRequests();

        var requests = (result.Result as OkObjectResult)!.Value as IEnumerable<ServiceRequestResponseDTO>;
        requests!.Select(r => r.Session_Id).Should().BeEquivalentTo(new[] { NorthSession, SouthSession });
    }

    // Creating: staff can't create things at another location

    [Fact]
    public async Task CreateTable_ForbidsStaffAtAnotherLocation()
    {
        var result = await Tables(StaffId, UserRoles.Staff).CreateTable(new TableEntityCreateDTO
        {
            Table_Number = 2, Seat_Count = 4, Is_Active = true, Location_Id = Southside,
        });

        ShouldBeForbidden(result);
        _context.Tables.Count(t => t.Location_Id == Southside).Should().Be(1);
    }

    // One item by ID: staff can't change another location's tables, sessions, orders or requests

    [Fact]
    public async Task UpdateTable_ForbidsStaffAtAnotherLocation()
    {
        var result = await Tables(StaffId, UserRoles.Staff).UpdateTable(SouthTable, new TableEntityUpdateDTO { Seat_Count = 8 });

        ShouldBeForbidden(result);
        (await _context.Tables.FindAsync(SouthTable))!.seat_count.Should().Be(4);
    }

    [Fact]
    public async Task UpdateTable_LetsStaffChangeTheirOwnLocation()
    {
        var result = await Tables(StaffId, UserRoles.Staff).UpdateTable(NorthTable, new TableEntityUpdateDTO { Seat_Count = 8 });

        result.Should().BeOfType<OkObjectResult>();
        (await _context.Tables.FindAsync(NorthTable))!.seat_count.Should().Be(8);
    }

    [Fact]
    public async Task UpdateTable_LetsAdminsChangeAnyLocation()
    {
        var result = await Tables(AdminId, UserRoles.Admin).UpdateTable(SouthTable, new TableEntityUpdateDTO { Seat_Count = 8 });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CloseSession_ForbidsStaffAtAnotherLocation()
    {
        var result = await Sessions(StaffId, UserRoles.Staff).close_dining_session(SouthSession);

        ShouldBeForbidden(result);
        (await _context.DiningSessions.FindAsync(SouthSession))!.Ended_At.Should().BeNull();
    }

    [Fact]
    public async Task CancelOrder_ForbidsStaffAtAnotherLocation()
    {
        var result = await Orders(StaffId, UserRoles.Staff).CancelOrder(SouthOrder);

        ShouldBeForbidden(result);
        (await _context.SessionOrders.FindAsync(SouthOrder))!.Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task CancelOrder_LetsStaffCancelAtTheirOwnLocation()
    {
        var result = await Orders(StaffId, UserRoles.Staff).CancelOrder(NorthOrder);

        result.Should().BeOfType<OkObjectResult>();
        (await _context.SessionOrders.FindAsync(NorthOrder))!.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task CompleteServiceRequest_ForbidsStaffAtAnotherLocation()
    {
        var result = await ServiceRequests(StaffId, UserRoles.Staff).CompleteServiceRequest(2);

        ShouldBeForbidden(result);
        (await _context.ServiceRequests.FindAsync(2))!.Status.Should().Be(ServiceRequestStatus.Pending);
    }

    [Fact]
    public async Task OrdersForASession_ForbidStaffAtAnotherLocation()
    {
        var result = await Orders(StaffId, UserRoles.Staff).GetOrderBySession(SouthSession);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    // The default menu row applies at every location, so only admins can change it

    [Fact]
    public async Task UpdateMenuAssignment_ForbidsStaffChangingEveryLocation()
    {
        var result = await MenuAssignments(StaffId, UserRoles.Staff)
            .Update_Menu_Item_Assignment(1, 1, new MenuAssignmentUpdateDTO { Price = 9.99m }, locationId: null);

        ShouldBeForbidden(result);
    }

    [Fact]
    public async Task UpdateMenuAssignment_ForbidsStaffAtAnotherLocation()
    {
        var result = await MenuAssignments(StaffId, UserRoles.Staff)
            .Update_Menu_Item_Assignment(1, 1, new MenuAssignmentUpdateDTO { Price = 9.99m }, locationId: Southside);

        ShouldBeForbidden(result);
    }
}
