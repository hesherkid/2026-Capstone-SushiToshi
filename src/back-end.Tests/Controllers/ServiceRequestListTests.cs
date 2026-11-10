using System.Security.Claims;
using System.Text.Json;
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
using back_end.Dtos;

namespace back_end.Tests.Controllers;

// GET /api/ServiceRequest: every request at the location, for staff
public class ServiceRequestListTests : IDisposable
{
    private const int Customer = 21, Server = 30;

    private readonly ApplicationDbContext _context;
    private readonly ServiceRequestController _controller;

    public ServiceRequestListTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _context.Tables.Add(new TableEntity { Table_Id = 101, table_number = 4, Location_Id = 1, is_active = true });
        _context.DiningSessions.Add(new DiningSession { Session_Id = 7, Menu_Id = 1, Location_Id = 1, Table_Id = 101, Started_At = DateTime.UtcNow });
        _context.Users.AddRange(
            new User { User_id = Customer, Email = "guest@example.com", Password_hash = "customer-hash", Role = UserRoles.Customer },
            new User { User_id = Server, Email = "server@example.com", Password_hash = "server-hash", Role = UserRoles.Staff, Location_id = 1 });
        _context.ServiceRequests.Add(new ServiceRequest
        {
            request_id = 1, Session_Id = 7, Table_Id = 101, Request_By = Customer, Claimed_By = Server,
            Notes = "More water", Status = ServiceRequestStatus.Pending,
        });
        _context.SaveChanges();

        _controller = new ServiceRequestController(_context, new Mock<ILogger<ServiceRequestController>>().Object, new LocationAccessService(_context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "1"),
                        new Claim(ClaimTypes.Role, nameof(UserRoles.Admin)),
                    }, "TestAuth")),
                },
            },
        };
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task GetAllServiceRequest_ReturnsRequestsWithoutTheirUsers()
    {
        // Act
        var result = await _controller.GetAllServiceRequest();

        // Assert
        var requests = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value.Should().BeOfType<List<ServiceRequestResponseDto>>().Subject;
        var request = requests.Single();
        request.RequestedBy.Should().Be(Customer);
        request.ClaimedBy.Should().Be(Server);
        request.TableNumber.Should().Be(4);

        // The entity's users would send their emails and password hashes
        var json = JsonSerializer.Serialize(requests);
        json.Should().NotContain("hash").And.NotContain("@example.com");
    }
}
