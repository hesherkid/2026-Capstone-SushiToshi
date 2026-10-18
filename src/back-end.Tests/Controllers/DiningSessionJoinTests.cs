using System.Security.Claims;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using back_end.controllers;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.DiningSessionDTOs;

namespace back_end.Tests.Controllers;

// POST /api/DiningSession/join (guests joining from the QR code) and staff-only session creation
public class DiningSessionJoinTests : IDisposable
{
    private const int LocationId = 1;
    private const int GuestUserId = 10;
    private const int MiaUserId = 11;

    private readonly ApplicationDbContext _context;

    public DiningSessionJoinTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _context.Locations.Add(new Locations { Location_Id = LocationId, Name = "Downtown" });
        _context.Menus.Add(new Menu { Menu_id = 1, Name = "Dinner" });
        _context.Users.AddRange(
            new User { User_id = GuestUserId, Email = "guestemail@email.com", First_name = "Guest", Last_name = "User", Role = UserRoles.Customer },
            new User { User_id = MiaUserId, Email = "mia@example.com", First_name = "Mia", Last_name = "Chen", Role = UserRoles.Customer }
        );
        _context.TableGroups.Add(new TableGroup { TableGroup_Id = 50, Group_Name = "Patio", Location_Id = LocationId });
        _context.Tables.AddRange(
            new TableEntity { Table_Id = 101, table_number = 1, Location_Id = LocationId, is_active = true },  // has a session
            new TableEntity { Table_Id = 102, table_number = 2, Location_Id = LocationId, is_active = true },  // free
            new TableEntity { Table_Id = 103, table_number = 3, Location_Id = LocationId, is_active = true, TableGroup_Id = 50 },
            new TableEntity { Table_Id = 104, table_number = 4, Location_Id = LocationId, is_active = true, TableGroup_Id = 50 },
            new TableEntity { Table_Id = 105, table_number = 5, Location_Id = LocationId, is_active = false }
        );
        _context.DiningSessions.AddRange(
            new DiningSession { Session_Id = 7, Menu_Id = 1, Location_Id = LocationId, Table_Id = 101, Started_At = DateTime.UtcNow },
            new DiningSession { Session_Id = 8, Menu_Id = 1, Location_Id = LocationId, TableGroup_Id = 50, Started_At = DateTime.UtcNow },
            // Ended sessions are never joined
            new DiningSession { Session_Id = 6, Menu_Id = 1, Location_Id = LocationId, Table_Id = 102, Started_At = DateTime.UtcNow.AddHours(-3), Ended_At = DateTime.UtcNow.AddHours(-2) }
        );
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private DiningSessionController ControllerAs(int? userId, string role = "Customer") =>
        new(_context, new Mock<IConfiguration>().Object, new Mock<ILogger<DashBoardController>>().Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = userId == null
                        ? new ClaimsPrincipal(new ClaimsIdentity())
                        : new ClaimsPrincipal(new ClaimsIdentity(new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, userId.ToString()!),
                            new Claim(ClaimTypes.Role, role),
                        }, "TestAuth")),
                },
            },
        };

    private static JoinTableSessionDTO Table(int number) => new() { Table_Number = number, Location_Id = LocationId };

    private static JoinTableSessionResponseDTO Body(IActionResult result) =>
        ((result as OkObjectResult)!.Value as JoinTableSessionResponseDTO)!;

    [Fact]
    public async Task Join_AddsTheSignedInUserToTheTablesSession()
    {
        var result = await ControllerAs(MiaUserId).JoinTableSession(Table(1));

        var body = Body(result);
        body.Session_Id.Should().Be(7);
        body.Menu_Id.Should().Be(1);
        body.Table_Numbers.Should().Equal(1);
        body.Already_Joined.Should().BeFalse();

        var participant = await _context.SessionParticipants.SingleAsync();
        participant.User_Id.Should().Be(MiaUserId);
        participant.User_Name.Should().Be("Mia Chen");
        body.Participant_Id.Should().Be(participant.Participant_Id);
    }

    [Fact]
    public async Task Join_IsSafeToRepeat()
    {
        var first = Body(await ControllerAs(MiaUserId).JoinTableSession(Table(1)));
        var second = Body(await ControllerAs(MiaUserId).JoinTableSession(Table(1)));

        second.Already_Joined.Should().BeTrue();
        second.Participant_Id.Should().Be(first.Participant_Id);
        (await _context.SessionParticipants.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Join_SeatsGroupedTablesInTheGroupsSession(int tableNumber)
    {
        var body = Body(await ControllerAs(GuestUserId).JoinTableSession(Table(tableNumber)));

        body.Session_Id.Should().Be(8);
    }

    [Fact]
    public async Task Join_NeverStartsASession()
    {
        var result = await ControllerAs(MiaUserId).JoinTableSession(Table(2));

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.Value!.ToString().Should().Contain("ask staff");
        (await _context.DiningSessions.CountAsync(ds => ds.Ended_At == null)).Should().Be(2);
        (await _context.SessionParticipants.AnyAsync()).Should().BeFalse();
    }

    [Theory]
    [InlineData(5)]   // inactive
    [InlineData(99)]  // doesn't exist
    public async Task Join_RejectsUnknownOrInactiveTables(int tableNumber)
    {
        var result = await ControllerAs(MiaUserId).JoinTableSession(Table(tableNumber));

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Join_RequiresASignedInUser()
    {
        var result = await ControllerAs(null).JoinTableSession(Table(1));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public void CreateSession_IsStaffOnly()
    {
        var method = typeof(DiningSessionController).GetMethod(nameof(DiningSessionController.create_dining_session))!;
        var authorize = method
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single();

        authorize.Roles.Should().Be("Admin,Staff");
    }

    [Theory]
    [InlineData(101)]  // table already has a session
    [InlineData(103)]  // table's group already has a session
    public async Task CreateSession_RefusesBusyTables(int tableId)
    {
        var result = await ControllerAs(1, "Staff").create_dining_session(new CreateSessionRequestDTO
        {
            Menu_Id = 1,
            Location_Id = LocationId,
            Table_Id = tableId,
            AssignmentType = "table",
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        (await _context.DiningSessions.CountAsync(ds => ds.Ended_At == null)).Should().Be(2);
    }

    [Fact]
    public async Task CreateSession_StartsASessionOnAFreeTableWithoutAddingParticipants()
    {
        var result = await ControllerAs(1, "Staff").create_dining_session(new CreateSessionRequestDTO
        {
            Menu_Id = 1,
            Location_Id = LocationId,
            Table_Id = 102,
            AssignmentType = "table",
        });

        var body = (result as OkObjectResult)!.Value as DiningSessionResponseDTO;
        body!.Table_Numbers.Should().Equal(2);
        (await _context.DiningSessions.AnyAsync(ds => ds.Table_Id == 102 && ds.Ended_At == null)).Should().BeTrue();
        (await _context.SessionParticipants.AnyAsync()).Should().BeFalse();
    }
}
