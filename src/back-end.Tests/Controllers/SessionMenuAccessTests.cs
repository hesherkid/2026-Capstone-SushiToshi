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
using back_end.Services;
using back_end.DTO.DiningSessionDTOs;

namespace back_end.Tests.Controllers;

// Tests for session menu access control based on user seating and role
public class SessionMenuAccessTests : IDisposable
{
    private const int LocationId = 1;
    private const int MiaUserId = 11;
    private const int StaffUserId = 2;

    private readonly ApplicationDbContext _context;

    public SessionMenuAccessTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _context.Locations.Add(new Locations { Location_Id = LocationId, Name = "North" });
        _context.Menus.AddRange(new Menu { Menu_id = 1, Name = "Dinner" }, new Menu { Menu_id = 2, Name = "Lunch" });
        _context.Users.AddRange(
            new User { User_id = MiaUserId, Email = "mia@example.com", First_name = "Mia", Last_name = "Goth", Role = UserRoles.Customer },
            new User { User_id = StaffUserId, Email = "staff@sushitoshi.ca", Role = UserRoles.Staff, Location_id = LocationId });
        _context.Tables.AddRange(
            new TableEntity { Table_Id = 101, table_number = 1, Location_Id = LocationId, is_active = true },
            new TableEntity { Table_Id = 102, table_number = 2, Location_Id = LocationId, is_active = true });
        _context.DiningSessions.AddRange(
            new DiningSession { Session_Id = 7, Menu_Id = 1, Location_Id = LocationId, Table_Id = 101, Started_At = DateTime.UtcNow.AddMinutes(-30) },
            new DiningSession { Session_Id = 8, Menu_Id = 2, Location_Id = LocationId, Table_Id = 102, Started_At = DateTime.UtcNow.AddMinutes(-10) });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private DiningSessionController ControllerAs(int userId, string role) =>
        new(_context, new Mock<IConfiguration>().Object, new Mock<ILogger<DashBoardController>>().Object, new LocationAccessService(_context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Role, role),
                    }, "TestAuth")),
                },
            },
        };

    private void SeatMia(int sessionId, DateTime? leftAt = null)
    {
        _context.SessionParticipants.Add(new SessionParticipant
        {
            Session_Id = sessionId,
            User_Id = MiaUserId,
            User_Name = "Mia Goth",
            Joined_At = DateTime.UtcNow.AddMinutes(-20),
            Left_At = leftAt
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task SeatedCustomer_GetsTheirTablesMenu()
    {
        SeatMia(7);

        var result = await ControllerAs(MiaUserId, "Customer").get_session_menu_id(7);

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(new SessionMenuResponseDTO { Menu_Id = 1, Location_Id = LocationId });
    }

    [Fact]
    public async Task Customer_CannotReadAnotherTablesMenu()
    {
        SeatMia(7);

        var result = await ControllerAs(MiaUserId, "Customer").get_session_menu_id(8);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Customer_CannotReadATableTheyLeft()
    {
        SeatMia(7, leftAt: DateTime.UtcNow.AddMinutes(-5));

        var result = await ControllerAs(MiaUserId, "Customer").get_session_menu_id(7);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Customer_CannotProbeForSessionsThatDontExist()
    {
        SeatMia(7);

        var result = await ControllerAs(MiaUserId, "Customer").get_session_menu_id(999);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Staff_CanReadAnyTablesMenu()
    {
        var result = await ControllerAs(StaffUserId, "Staff").get_session_menu_id(8);

        result.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeEquivalentTo(new SessionMenuResponseDTO { Menu_Id = 2, Location_Id = LocationId });
    }

    [Fact]
    public async Task Staff_GetNotFoundForAMissingSession()
    {
        var result = await ControllerAs(StaffUserId, "Staff").get_session_menu_id(999);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }
}