using System.Security.Claims;
using back_end.Services;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.Controllers;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.DTO.ServiceRequestDTOs;

namespace back_end.Tests.Controllers;

// POST /api/ServiceRequest/{session}: a table waits 30 seconds between requests, whoever at it sends them
public class ServiceRequestCooldownTests : IDisposable
{
    private const int Guest = 21;
    private const int OtherGuest = 22;
    private const int SessionId = 7;

    private readonly ApplicationDbContext _context;

    public ServiceRequestCooldownTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _context.Tables.Add(new TableEntity { Table_Id = 101, table_number = 1, Location_Id = 1, is_active = true });
        _context.DiningSessions.Add(new DiningSession { Session_Id = SessionId, Menu_Id = 1, Location_Id = 1, Table_Id = 101, Started_At = DateTime.UtcNow });
        _context.Users.AddRange(
            new User { User_id = Guest, Email = "guest1@example.com", Role = UserRoles.Customer },
            new User { User_id = OtherGuest, Email = "guest2@example.com", Role = UserRoles.Customer });
        _context.SessionParticipants.AddRange(
            new SessionParticipant { Session_Id = SessionId, User_Id = Guest, User_Name = "Guest K7P2" },
            new SessionParticipant { Session_Id = SessionId, User_Id = OtherGuest, User_Name = "Guest 4MXA" });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private ServiceRequestController ControllerAs(int userId) =>
        new(_context, new Mock<ILogger<ServiceRequestController>>().Object, new LocationAccessService(_context))
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

    private Task<IActionResult> Send(int userId, string notes = "Need napkins") =>
        ControllerAs(userId).CreateServiceRequest(SessionId, new ServiceRequestCreateDTO { Notes = notes });

    private void AddEarlierRequest(string notes, TimeSpan ago)
    {
        _context.ServiceRequests.Add(new ServiceRequest
        {
            Session_Id = SessionId, Table_Id = 101, Request_By = Guest,
            Notes = notes, Status = ServiceRequestStatus.Pending, Created_At = DateTime.UtcNow - ago,
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task FirstRequest_IsSent()
    {
        var result = await Send(Guest);

        result.Should().BeOfType<OkObjectResult>();
        _context.ServiceRequests.Should().ContainSingle();
    }

    [Fact]
    public async Task SecondRequestRightAway_IsRefusedWithTimeLeft()
    {
        await Send(Guest);

        var result = await Send(Guest, "Need wasabi");

        var refused = result.Should().BeOfType<ObjectResult>().Subject;
        refused.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        var retryAfter = (int)refused.Value!.GetType().GetProperty("retry_after_seconds")!.GetValue(refused.Value)!;
        retryAfter.Should().BeInRange(1, 30);
        _context.ServiceRequests.Should().ContainSingle();
    }

    [Fact]
    public async Task AnotherGuestAtTheSameTable_SharesTheCooldown()
    {
        await Send(Guest);

        var result = await Send(OtherGuest);

        (result as ObjectResult)!.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public async Task RequestAfterTheCooldown_IsSent()
    {
        AddEarlierRequest("Need napkins", ServiceRequestController.RequestCooldown + TimeSpan.FromSeconds(1));

        var result = await Send(Guest);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AskingForTheBill_DoesNotStartTheCooldown()
    {
        AddEarlierRequest($"{ServiceRequestController.BillRequestNotePrefix} Lee", TimeSpan.FromSeconds(5));

        var result = await Send(Guest);

        result.Should().BeOfType<OkObjectResult>();
    }
}
