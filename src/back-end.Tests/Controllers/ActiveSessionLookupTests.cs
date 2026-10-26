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

namespace back_end.Tests.Controllers;

// Tests for looking up the active dining session for a user based on their seating status
public class ActiveSessionLookupTests : IDisposable
{
    private const int LocationId = 1;
    private const int MiaUserId = 11;

    private readonly ApplicationDbContext _context;

    public ActiveSessionLookupTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _context.Locations.Add(new Locations { Location_Id = LocationId, Name = "North" });
        _context.Menus.Add(new Menu { Menu_id = 1, Name = "Dinner" });
        _context.Users.Add(new User { User_id = MiaUserId, Email = "mia@example.com", First_name = "Mia", Last_name = "Goth", Role = UserRoles.Customer });
        _context.Tables.AddRange(
            new TableEntity { Table_Id = 101, table_number = 1, Location_Id = LocationId, is_active = true },
            new TableEntity { Table_Id = 102, table_number = 2, Location_Id = LocationId, is_active = true });
        _context.DiningSessions.AddRange(
            new DiningSession { Session_Id = 7, Menu_Id = 1, Location_Id = LocationId, Table_Id = 101, Started_At = DateTime.UtcNow.AddMinutes(-30) },
            new DiningSession { Session_Id = 8, Menu_Id = 1, Location_Id = LocationId, Table_Id = 102, Started_At = DateTime.UtcNow.AddMinutes(-10) });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private DiningSessionController ControllerAs(int userId) =>
        new(_context, new Mock<IConfiguration>().Object, new Mock<ILogger<DashBoardController>>().Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Role, "Customer"),
                    }, "TestAuth")),
                },
            },
        };

    private void Seat(int sessionId, DateTime? leftAt = null) =>
        _context.SessionParticipants.Add(new SessionParticipant
        {
            Session_Id = sessionId,
            User_Id = MiaUserId,
            User_Name = "Mia Goth",
            Joined_At = DateTime.UtcNow.AddMinutes(-20),
            Left_At = leftAt
        });

    private Task<IActionResult> LookUp() => ControllerAs(MiaUserId).GetActiveSessionId();

    [Fact]
    public async Task ReturnsTheTableTheUserIsSeatedAt()
    {
        Seat(7);
        await _context.SaveChangesAsync();

        var result = await LookUp();
        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(new { session_id = 7 });
    }

    [Fact]
    public async Task IgnoresATableTheUserHasLeft()
    {
        Seat(7, leftAt: DateTime.UtcNow.AddMinutes(-5));
        await _context.SaveChangesAsync();

        var result = await LookUp();
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AfterMovingTables_ReturnsTheNewTableNotTheOldOne()
    {
        Seat(8, leftAt: DateTime.UtcNow.AddMinutes(-2));
        Seat(7);
        await _context.SaveChangesAsync();

        var result = await LookUp();
        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(new { session_id = 7 });
    }

    [Fact]
    public async Task IgnoresSessionsThatHaveEnded()
    {
        Seat(7);
        _context.DiningSessions.Single(s => s.Session_Id == 7).Ended_At = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var result = await LookUp();
        result.Should().BeOfType<NotFoundObjectResult>();
    }

}
