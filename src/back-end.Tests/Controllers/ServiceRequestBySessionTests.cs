using System.Security.Claims;
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

// GET /api/ServiceRequest/by-session/{id}: staff see any table; customers only the table they're seated at
public class ServiceRequestBySessionTests : IDisposable
{
    private const int SeatedCustomer = 21;
    private const int OtherCustomer = 22;
    private const int StaffMember = 30;

    private readonly ApplicationDbContext _context;

    public ServiceRequestBySessionTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _context.Tables.Add(new TableEntity { Table_Id = 101, table_number = 1, Location_Id = 1, is_active = true });
        _context.DiningSessions.Add(new DiningSession { Session_Id = 7, Menu_Id = 1, Location_Id = 1, Table_Id = 101, Started_At = DateTime.UtcNow });
        _context.SessionParticipants.AddRange(
            new SessionParticipant { Session_Id = 7, User_Id = SeatedCustomer, User_Name = "Guest K7P2" },
            // Left the table, so no longer counts as seated
            new SessionParticipant { Session_Id = 7, User_Id = OtherCustomer, User_Name = "Guest 4MXA", Left_At = DateTime.UtcNow });
        _context.ServiceRequests.Add(new ServiceRequest
        {
            request_id = 1, Session_Id = 7, Table_Id = 101, Request_By = SeatedCustomer,
            Notes = "Ready for bill: Lee", Status = ServiceRequestStatus.Pending,
        });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private ServiceRequestController ControllerAs(int userId, string role) =>
        new(_context, new Mock<ILogger<ServiceRequestController>>().Object)
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

    [Fact]
    public async Task SeatedCustomer_CanSeeTheirTablesPendingRequests()
    {
        var result = await ControllerAs(SeatedCustomer, "Customer").GetSessionServiceRequest(7);

        var requests = (result as OkObjectResult)!.Value as List<ServiceRequestResponseDTO>;
        requests!.Single().Notes.Should().Be("Ready for bill: Lee");
    }

    [Fact]
    public async Task CustomerNotSeatedAtTheTable_IsRefused()
    {
        var result = await ControllerAs(OtherCustomer, "Customer").GetSessionServiceRequest(7);

        result.Should().BeOfType<ForbidResult>();
    }

    [Theory]
    [InlineData("Staff")]
    [InlineData("Admin")]
    public async Task StaffAndAdmins_CanSeeAnyTable(string role)
    {
        var result = await ControllerAs(StaffMember, role).GetSessionServiceRequest(7);

        result.Should().BeOfType<OkObjectResult>();
    }
}
