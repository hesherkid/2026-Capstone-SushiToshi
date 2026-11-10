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
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Dtos;
using back_end.Services;

namespace back_end.Tests.Controllers;

// Staff location lock is tested separately in StaffLocationLockTests. Bill details are tested in BillLinkTests.

public class DashboardControllerTests : IDisposable
{
    private const int LocationId = 1;
    private const int OtherLocationId = 2;
    private const int NoSuchLocationId = 99;
    private const int StaffId = 1;
    private const int AdminId = 2;
    private const int UnassignedStaffId = 3;
    private const int Table1 = 11;
    private const int Table2 = 12;
    private const int GroupTableA = 13;
    private const int GroupTableB = 14;
    private const int OtherTable = 21;
    private const int GroupId = 50;
    private const int SessionId = 100;
    private const int OtherSessionId = 200;
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<DashboardController>> _logger = new();

    public DashboardControllerTests()
    {
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        _context.Locations.AddRange(
            new Locations { Location_Id = LocationId, Name = "Downtown" },
            new Locations { Location_Id = OtherLocationId, Name = "Southside" });
        _context.Menus.Add(new Menu { Menu_id = 1, Name = "Dinner" });
        _context.Users.AddRange(
            new User { User_id = StaffId, Email = "staff@example.com", Role = UserRoles.Staff, Location_id = LocationId },
            new User { User_id = AdminId, Email = "admin@example.com", Role = UserRoles.Admin, Location_id = LocationId },
            new User { User_id = UnassignedStaffId, Email = "new@example.com", Role = UserRoles.Staff });
        _context.TableGroups.Add(new TableGroup { TableGroup_Id = GroupId, Group_Name = "Patio", Location_Id = LocationId, Is_Active = true });
        _context.Tables.AddRange(
            new TableEntity { Table_Id = Table1, table_number = 1, seat_count = 4, Location_Id = LocationId, is_active = true },
            new TableEntity { Table_Id = Table2, table_number = 2, seat_count = 4, Location_Id = LocationId, is_active = true },
            new TableEntity { Table_Id = GroupTableA, table_number = 3, seat_count = 2, Location_Id = LocationId, is_active = true, TableGroup_Id = GroupId },
            new TableEntity { Table_Id = GroupTableB, table_number = 4, seat_count = 6, Location_Id = LocationId, is_active = true, TableGroup_Id = GroupId },
            new TableEntity { Table_Id = OtherTable, table_number = 1, seat_count = 4, Location_Id = OtherLocationId, is_active = true });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private DashboardController Controller(int userId = StaffId, UserRoles role = UserRoles.Staff)
    {
        _context.ChangeTracker.Clear();

        return new(_context, new Mock<IConfiguration>().Object, _logger.Object, new LocationAccessService(_context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Role, role.ToString()),
                    }, "TestAuth")),
                },
            },
        };
    }

    private void AddSession(int id = SessionId, int? tableId = Table1, int? groupId = null,
        int locationId = LocationId, bool ended = false, int minutesAgo = 60)
    {
        _context.DiningSessions.Add(new DiningSession
        {
            Session_Id = id,
            Menu_Id = 1,
            Location_Id = locationId,
            Table_Id = tableId,
            TableGroup_Id = groupId,
            Started_At = DateTime.UtcNow.AddMinutes(-minutesAgo),
            Ended_At = ended ? DateTime.UtcNow.AddMinutes(-5) : null,
        });
        _context.SaveChanges();
    }

    private void AddBill(int billId, int sessionId, BillStatus status)
    {
        _context.Bills.Add(new Billing { Bill_Id = billId, Session_Id = sessionId, Bill_Name = "Bill", Status = status, Adult_Count = 1, Total_Count = 1 });
        _context.SaveChanges();
    }

    private void AddSeat(int sessionId, string name, int? billId = null, int joinedMinutesAgo = 30, bool left = false)
    {
        _context.SessionParticipants.Add(new SessionParticipant
        {
            Session_Id = sessionId,
            User_Name = name,
            Bill_Id = billId,
            Joined_At = DateTime.UtcNow.AddMinutes(-joinedMinutesAgo),
            Left_At = left ? DateTime.UtcNow.AddMinutes(-5) : null,
        });
        _context.SaveChanges();
    }

    private void AddBusyEvening()
    {
        AddSession(SessionId, Table1);
        AddBill(1, SessionId, BillStatus.Open);
        AddBill(2, SessionId, BillStatus.Paid);
        AddSeat(SessionId, "Ana", billId: 1);
        AddSeat(SessionId, "Ben");
        AddSeat(SessionId, "Cy", billId: 1, left: true);

        AddSession(101, tableId: null, groupId: GroupId);
        AddBill(3, 101, BillStatus.Closed);
        AddSeat(101, "Dee", billId: 3);

        AddSession(102, Table2, ended: true);
        AddBill(4, 102, BillStatus.Open);
        AddSeat(102, "Eve");

        AddSession(OtherSessionId, OtherTable, locationId: OtherLocationId);
        AddBill(5, OtherSessionId, BillStatus.Open);
        AddSeat(OtherSessionId, "Fay", billId: 5);
    }

    private static List<DashboardSessionsDto> Sessions(IActionResult result) =>
        (List<DashboardSessionsDto>)result.Should().BeOfType<OkObjectResult>().Subject.Value!;

    private static DashboardSummaryDto Summary(IActionResult result) =>
        (DashboardSummaryDto)result.Should().BeOfType<OkObjectResult>().Subject.Value!;


    [Fact]
    public async Task GetSessions_NewestFirst_LeavesOutEndedSessions()
    {
        AddSession(SessionId, Table1, minutesAgo: 60);
        AddSession(101, Table2, minutesAgo: 10);
        AddSession(102, tableId: null, groupId: GroupId, ended: true);

        var result = await Controller().GetDashboardSessions(null);

        Sessions(result).Select(s => s.SessionId).Should().Equal(101, SessionId);
    }

    [Fact]
    public async Task GetSessions_AdminsSeeEveryLocation()
    {
        AddSession(SessionId, Table1);
        AddSession(OtherSessionId, OtherTable, locationId: OtherLocationId);

        var result = await Controller(AdminId, UserRoles.Admin).GetDashboardSessions(null);

        Sessions(result).Select(s => s.SessionId).Should().BeEquivalentTo(new[] { SessionId, OtherSessionId });
    }

    [Fact]
    public async Task GetSessions_AdminsSeeOnlyTheLocationTheyWant()
    {
        AddSession(SessionId, Table1);
        AddSession(OtherSessionId, OtherTable, locationId: OtherLocationId);

        var result = await Controller(AdminId, UserRoles.Admin).GetDashboardSessions(OtherLocationId);

        var session = Sessions(result).Should().ContainSingle().Subject;
        session.SessionId.Should().Be(OtherSessionId);
        session.LocationName.Should().Be("Southside");
    }

    [Fact]
    public async Task GetSessions_NoActiveSessions_ReturnsNoContent()
    {
        AddSession(ended: true);

        var result = await Controller().GetDashboardSessions(null);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task GetSessions_StaffWithNoLocation_ReturnsForbidden()
    {
        AddSession();

        var result = await Controller(UnassignedStaffId).GetDashboardSessions(LocationId);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task GetSessions_SingleTable_ShowsNumberAndSeats()
    {
        AddSession(SessionId, Table1);

        var result = await Controller().GetDashboardSessions(null);

        var session = Sessions(result).Single();
        session.MenuId.Should().Be(1);
        session.LocationId.Should().Be(LocationId);
        session.LocationName.Should().Be("Downtown");
        session.TableNumbers.Should().Equal(1);
        session.SeatCount.Should().Be(4);
    }

    [Fact]
    public async Task GetSessions_TableGroup_ListsEveryTable_AddsUpTheirSeats()
    {
        AddSession(SessionId, tableId: null, groupId: GroupId);

        var result = await Controller().GetDashboardSessions(null);

        var session = Sessions(result).Single();
        session.TableNumbers.Should().BeEquivalentTo(new[] { 3, 4 });
        session.SeatCount.Should().Be(8);
    }

    [Fact]
    public async Task GetSessions_ListsOnlyPeopleStillSeated_InTheOrderTheyJoined()
    {
        AddSession();
        AddBill(1, SessionId, BillStatus.Open);
        AddSeat(SessionId, "Ben", joinedMinutesAgo: 20);
        AddSeat(SessionId, "Ana", billId: 1, joinedMinutesAgo: 25);
        AddSeat(SessionId, "Cy", billId: 1, joinedMinutesAgo: 28, left: true);

        var result = await Controller().GetDashboardSessions(null);

        var session = Sessions(result).Single();
        session.ActiveParticipants.Should().Be(2);
        session.People.Select(p => p.UserName).Should().Equal("Ana", "Ben");
        session.People.Single(p => p.UserName == "Ana").BillId.Should().Be(1);
        session.People.Single(p => p.UserName == "Ben").BillId.Should().BeNull();
        session.Bills.Single().PeopleOnBill.Should().Be(1);
    }

    [Fact]
    public async Task GetSessions_ReturnsBillDetails()
    {
        var createdAt = DateTime.UtcNow.AddMinutes(-50);
        var requestedAt = DateTime.UtcNow.AddMinutes(-1);
        AddSession();
        _context.Bills.Add(new Billing
        {
            Bill_Id = 1,
            Session_Id = SessionId,
            Bill_Name = "Lee",
            Status = BillStatus.Open,
            Adult_Count = 1,
            Senior_Count = 1,
            Child_Count = 1,
            Tot_Count = 1,
            Total_Count = 4,
            Created_At = createdAt,
            Requested_At = requestedAt,
            Started_By_Guest = true,
            Counts_Changed_By_Guest = true,
            Counts_Locked = true,
        });
        _context.SaveChanges();
        AddSeat(SessionId, "Ana", billId: 1);

        var result = await Controller().GetDashboardSessions(null);

        var session = Sessions(result).Single();
        session.BillCount.Should().Be(1);
        session.Bills.Single().Should().BeEquivalentTo(new DashboardBillDto
        {
            BillId = 1,
            BillName = "Lee",
            Status = BillStatus.Open,
            CreatedAt = createdAt,
            TotalGuests = 4,
            AdultCount = 1,
            SeniorCount = 1,
            ChildCount = 1,
            TotCount = 1,
            RequestedAt = requestedAt,
            StartedByGuest = true,
            CountsChangedByGuest = true,
            CountsLocked = true,
            PeopleOnBill = 1,
        });
    }

    [Theory]
    [InlineData(BillStatus.Open, false)]
    [InlineData(BillStatus.Closed, true)]
    [InlineData(BillStatus.Paid, true)]
    [InlineData(BillStatus.Cancelled, true)]
    public async Task GetSessions_Closable_WhenNoBillIsOpen(BillStatus status, bool closable)
    {
        AddSession();
        AddBill(1, SessionId, BillStatus.Paid);
        AddBill(2, SessionId, status);

        var result = await Controller().GetDashboardSessions(null);

        Sessions(result).Single().IsClosable.Should().Be(closable);
    }

    [Fact]
    public async Task GetSummary_AdminsSeeEveryLocation()
    {
        AddBusyEvening();

        var result = await Controller(AdminId, UserRoles.Admin).GetDashboardSummary(null);

        Summary(result).Should().BeEquivalentTo(new DashboardSummaryDto
        {
            LocationId = null,
            LocationName = null,
            TotalActiveSessions = 3,
            TotalTablesInUse = 4,
            TotalActiveBills = 2,
            TotalActiveParticipants = 4,
        });
    }

    [Fact]
    public async Task GetSummary_AdminsSeeOnlyTheLocationTheyWant()
    {
        AddBusyEvening();

        var result = await Controller(AdminId, UserRoles.Admin).GetDashboardSummary(LocationId);

        Summary(result).Should().BeEquivalentTo(new DashboardSummaryDto
        {
            LocationId = LocationId,
            LocationName = "Downtown",
            TotalActiveSessions = 2,
            TotalTablesInUse = 3,
            TotalActiveBills = 1,
            TotalActiveParticipants = 3,
        });
    }

    [Fact]
    public async Task GetSummary_StaffGetTheirOwnLocation()
    {
        AddBusyEvening();

        var result = await Controller().GetDashboardSummary(OtherLocationId);

        var summary = Summary(result);
        summary.LocationId.Should().Be(LocationId);
        summary.TotalActiveSessions.Should().Be(2);
    }

    [Fact]
    public async Task GetSummary_StaffWithNoLocation_ReturnsForbidden()
    {
        var result = await Controller(UnassignedStaffId).GetDashboardSummary(null);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Errors_AreLoggedWithoutSendingExceptionDetails()
    {
        AddSession();
        var controller = Controller();
        _context.Dispose();

        var results = new IActionResult[]
        {
            await controller.GetDashboardSessions(null),
            await controller.GetDashboardSummary(null),
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