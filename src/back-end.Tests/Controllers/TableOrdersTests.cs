using System.Security.Claims;
using System.Text.Json;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.Controllers;
using back_end.Services;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;

namespace back_end.Tests.Controllers;

// GET /api/Order/session/{id}: everyone seated at a table sees all of its orders
public class TableOrdersTests : IDisposable
{
    private const int Orderer = 21;      // ordered for the whole table
    private const int Friend = 22;       // same table, ordered nothing
    private const int OtherTable = 23;

    private readonly ApplicationDbContext _context;

    public TableOrdersTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _context.DiningSessions.AddRange(
            new DiningSession { Session_Id = 269, Menu_Id = 1, Location_Id = 1, Table_Id = 6, Started_At = DateTime.UtcNow },
            new DiningSession { Session_Id = 143, Menu_Id = 1, Location_Id = 1, Table_Id = 19, Started_At = DateTime.UtcNow });
        _context.SessionParticipants.AddRange(
            new SessionParticipant { Session_Id = 269, User_Id = Orderer, User_Name = "Guest K7P2" },
            new SessionParticipant { Session_Id = 269, User_Id = Friend, User_Name = "Guest 4MXA" },
            new SessionParticipant { Session_Id = 143, User_Id = OtherTable, User_Name = "Mia Chen" });
        _context.Bills.AddRange(
            new Billing { Bill_Id = 332, Session_Id = 269, Bill_Name = "Lee" },
            new Billing { Bill_Id = 333, Session_Id = 269, Bill_Name = "Park" });
        _context.SessionOrders.AddRange(
            new SessionOrder { Order_Id = 900, session_id = 269, Bill_Id = 332, User_Id = Orderer, User_Name = "Guest K7P2", Status = OrderStatus.Pending, Created_At = DateTime.UtcNow },
            new SessionOrder { Order_Id = 901, session_id = 269, Bill_Id = 333, User_Id = Orderer, User_Name = "Guest K7P2", Status = OrderStatus.Delivered, Created_At = DateTime.UtcNow });
        // Staff only see sessions at the location they're working at
        _context.Users.Add(new User { User_id = 30, Email = "staff@example.com", Role = UserRoles.Staff, Location_id = 1 });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private OrderController ControllerFor(int userId, string role = "Customer") =>
        new(_context, new Mock<ILogger<OrderController>>().Object, new LocationAccessService(_context))
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

    private static JsonElement Orders(ActionResult<IEnumerable<object>> result) =>
        JsonSerializer.SerializeToElement((result.Result as OkObjectResult)!.Value);

    [Fact]
    public async Task EveryoneAtTheTable_SeesOrdersPlacedForThem()
    {
        var orders = Orders(await ControllerFor(Friend).GetOrderBySession(269));

        orders.GetArrayLength().Should().Be(2);
        orders.EnumerateArray().Select(o => o.GetProperty("userName").GetString()).Should().AllBe("Guest K7P2");
        orders.EnumerateArray().Select(o => o.GetProperty("billName").GetString()).Should().BeEquivalentTo("Lee", "Park");
    }

    [Fact]
    public async Task TableOrders_CanBeFilteredByBill()
    {
        var orders = Orders(await ControllerFor(Friend).GetOrderBySession(269, bill_id: 333));

        orders.EnumerateArray().Select(o => o.GetProperty("orderId").GetInt32()).Should().Equal(901);
    }

    [Fact]
    public async Task CustomersAtAnotherTable_AreRefused()
    {
        var result = await ControllerFor(OtherTable).GetOrderBySession(269);

        result.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Staff_SeeAnyTableAtTheirLocation()
    {
        var orders = Orders(await ControllerFor(30, "Staff").GetOrderBySession(269));

        orders.GetArrayLength().Should().Be(2);
    }
}
