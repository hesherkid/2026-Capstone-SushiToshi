using System.Security.Claims;
using System.Text.Json;
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
using back_end.DTO.OrdersDTOs;
using back_end.Services;

namespace back_end.Tests.Controllers;

// "Ready for Bill" locks the bill: guests can't order more on it, staff still can
public class BillRequestTests : IDisposable
{
    private const int Guest = 21;
    private const int OtherTableGuest = 22;
    private const int Server = 30;

    private readonly ApplicationDbContext _context;

    public BillRequestTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _context.Tables.AddRange(
            new TableEntity { Table_Id = 6, table_number = 6, Location_Id = 1, is_active = true },
            new TableEntity { Table_Id = 19, table_number = 19, Location_Id = 1, is_active = true });
        _context.DiningSessions.AddRange(
            new DiningSession { Session_Id = 269, Menu_Id = 1, Location_Id = 1, Table_Id = 6, Started_At = DateTime.UtcNow },
            new DiningSession { Session_Id = 143, Menu_Id = 1, Location_Id = 1, Table_Id = 19, Started_At = DateTime.UtcNow });
        _context.SessionParticipants.AddRange(
            new SessionParticipant { Session_Id = 269, User_Id = Guest, User_Name = "Guest K7P2" },
            new SessionParticipant { Session_Id = 143, User_Id = OtherTableGuest, User_Name = "Guest 4MXA" });
        _context.Bills.AddRange(
            new Billing { Bill_Id = 332, Session_Id = 269, Bill_Name = "Lee", Adult_Count = 2, Total_Count = 2 },
            // Second party at the same table keeps ordering after the first asks for their bill
            new Billing { Bill_Id = 333, Session_Id = 269, Bill_Name = "Park", Adult_Count = 2, Total_Count = 2 },
            new Billing { Bill_Id = 334, Session_Id = 269, Bill_Name = "Closed", Status = BillStatus.Closed });
        _context.SessionOrders.Add(new SessionOrder { Order_Id = 900, session_id = 269, Bill_Id = 332, Status = OrderStatus.Pending, Created_At = DateTime.UtcNow });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private static ControllerContext As(int userId, string role) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role),
            }, "TestAuth")),
        },
    };

    private BillController Bills(int userId, string role = "Customer") =>
        new(_context, new Mock<IConfiguration>().Object, new Mock<ILogger<BillController>>().Object, new Mock<IPricingService>().Object)
        { ControllerContext = As(userId, role) };

    private OrderController Orders(int userId, string role = "Customer") =>
        new(_context, new Mock<ILogger<OrderController>>().Object, new LocationAccessService(_context)) { ControllerContext = As(userId, role) };

    private static JsonElement Body(IActionResult result) =>
        JsonSerializer.SerializeToElement((result as OkObjectResult)!.Value);

    [Fact]
    public async Task RequestBill_MarksTheBillAndNotifiesStaff()
    {
        var result = await Bills(Guest).Request_Bill(269, 332);

        Body(result).GetProperty("already_requested").GetBoolean().Should().BeFalse();
        (await _context.Bills.FindAsync(332))!.Requested_At.Should().NotBeNull();
        var request = await _context.ServiceRequests.SingleAsync();
        request.Notes.Should().Be("Ready for bill: Lee");
        request.Table_Id.Should().Be(6);
        request.Request_By.Should().Be(Guest);
    }

    [Fact]
    public async Task RequestBill_IsSafeToRepeat()
    {
        await Bills(Guest).Request_Bill(269, 332);
        var again = await Bills(Guest).Request_Bill(269, 332);

        Body(again).GetProperty("already_requested").GetBoolean().Should().BeTrue();
        (await _context.ServiceRequests.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RequestBill_RefusesClosedBillsAndOtherTables()
    {
        (await Bills(Guest).Request_Bill(269, 334)).Should().BeOfType<BadRequestObjectResult>();
        (await Bills(OtherTableGuest).Request_Bill(269, 332)).Should().BeOfType<ForbidResult>();
        (await _context.Bills.FindAsync(332))!.Requested_At.Should().BeNull();
    }

    [Fact]
    public async Task GuestsCantStartANewOrderOnARequestedBill()
    {
        await Bills(Guest).Request_Bill(269, 332);

        var result = await Orders(Guest).CreateOrder(new OrderCreateDTO { Session_Id = 269, Bill_Id = 332 });

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GuestsCantAddItemsToAnOrderOnARequestedBill()
    {
        await Bills(Guest).Request_Bill(269, 332);

        var result = await Orders(Guest).AddOrderItems(900, new List<OrderItemCreateDTO>
        {
            new() { Order_Id = 900, Menu_Id = 1, Item_Id = 1, Quantity = 1 },
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        (await _context.OrderItems.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task OtherBillsAtTheTable_CanStillOrder()
    {
        await Bills(Guest).Request_Bill(269, 332);

        var result = await Orders(Guest).CreateOrder(new OrderCreateDTO { Session_Id = 269, Bill_Id = 333 });

        result.Result.Should().NotBeOfType<BadRequestObjectResult>();
        (await _context.SessionOrders.AnyAsync(o => o.Bill_Id == 333)).Should().BeTrue();
    }

    [Fact]
    public async Task StaffCanStillAddToARequestedBill()
    {
        await Bills(Guest).Request_Bill(269, 332);

        var result = await Orders(Server, "Staff").CreateOrder(new OrderCreateDTO { Session_Id = 269, Bill_Id = 332 });

        result.Result.Should().NotBeOfType<BadRequestObjectResult>();
    }
}
