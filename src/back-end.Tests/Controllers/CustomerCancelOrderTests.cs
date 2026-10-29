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
using back_end.Services;

namespace back_end.Tests.Controllers;

// Customers (guests included) can cancel their own pending orders while seated at the table
public class CustomerCancelOrderTests : IDisposable
{
    private const int Location = 1;
    private const int Table = 11;
    private const int Session = 100;

    private const int EmmaId = 1;  // seated
    private const int LeeId = 2;   // seated at the same table
    private const int ParkId = 3;  // left the table

    private const int EmmasOrder = 1000;
    private const int LeesOrder = 1001;
    private const int EmmasServedOrder = 1002;
    private const int ParksOrder = 1003;

    private readonly ApplicationDbContext _context;

    public CustomerCancelOrderTests()
    {
        _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        _context.Locations.Add(new Locations { Location_Id = Location, Name = "Northside" });
        _context.Users.AddRange(
            new User { User_id = EmmaId, Email = "emma@example.com", Role = UserRoles.Customer },
            new User { User_id = LeeId, Email = "lee@example.com", Role = UserRoles.Customer },
            new User { User_id = ParkId, Email = "park@example.com", Role = UserRoles.Customer });
        _context.Menus.Add(new Menu { Menu_id = 1, Name = "Dinner" });
        _context.Tables.Add(new TableEntity { Table_Id = Table, table_number = 1, seat_count = 4, Location_Id = Location, is_active = true });
        _context.DiningSessions.Add(new DiningSession { Session_Id = Session, Menu_Id = 1, Location_Id = Location, Table_Id = Table, Started_At = DateTime.UtcNow });
        _context.SessionParticipants.AddRange(
            new SessionParticipant { Participant_Id = 1, Session_Id = Session, User_Id = EmmaId },
            new SessionParticipant { Participant_Id = 2, Session_Id = Session, User_Id = LeeId },
            new SessionParticipant { Participant_Id = 3, Session_Id = Session, User_Id = ParkId, Left_At = DateTime.UtcNow });
        _context.SessionOrders.AddRange(
            Order(EmmasOrder, EmmaId, OrderStatus.Pending),
            Order(LeesOrder, LeeId, OrderStatus.Pending),
            Order(EmmasServedOrder, EmmaId, OrderStatus.Delivered),
            Order(ParksOrder, ParkId, OrderStatus.Pending));
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private static SessionOrder Order(int orderId, int userId, OrderStatus status) =>
        new() { Order_Id = orderId, session_id = Session, User_Id = userId, User_Name = "Guest", Status = status, Created_At = DateTime.UtcNow };

    private OrderController OrdersAs(int userId) =>
        new(_context, new Mock<ILogger<OrderController>>().Object, new LocationAccessService(_context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Role, UserRoles.Customer.ToString()),
                    }, "TestAuth")),
                },
            },
        };

    private async Task<OrderStatus> StatusOf(int orderId) =>
        (await _context.SessionOrders.FindAsync(orderId))!.Status;

    [Fact]
    public async Task CancelOrder_LetsCustomerCancelTheirOwnPendingOrder()
    {
        var result = await OrdersAs(EmmaId).CancelOrder(EmmasOrder);

        result.Should().BeOfType<OkObjectResult>();
        (await StatusOf(EmmasOrder)).Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task CancelOrder_ForbidsCancellingSomeoneElsesOrderAtTheTable()
    {
        var result = await OrdersAs(EmmaId).CancelOrder(LeesOrder);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await StatusOf(LeesOrder)).Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task CancelOrder_ForbidsCustomerWhoLeftTheTable()
    {
        var result = await OrdersAs(ParkId).CancelOrder(ParksOrder);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await StatusOf(ParksOrder)).Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task CancelOrder_RefusesAnOrderThatWasAlreadyServed()
    {
        var result = await OrdersAs(EmmaId).CancelOrder(EmmasServedOrder);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await StatusOf(EmmasServedOrder)).Should().Be(OrderStatus.Delivered);
    }
}
