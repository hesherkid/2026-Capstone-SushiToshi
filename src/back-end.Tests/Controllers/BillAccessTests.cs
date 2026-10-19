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
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.Services;

namespace back_end.Tests.Controllers;

// Customers can only read the bills of the table they're seated at
public class BillAccessTests : IDisposable
{
    private const int SeatedAtTable6 = 21;
    private const int SeatedAtTable19 = 22;

    private readonly ApplicationDbContext _context;

    public BillAccessTests()
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
            new SessionParticipant { Session_Id = 269, User_Id = SeatedAtTable6, User_Name = "Guest K7P2" },
            new SessionParticipant { Session_Id = 143, User_Id = SeatedAtTable19, User_Name = "Guest 4MXA" });
        _context.Bills.AddRange(
            new Billing { Bill_Id = 332, Session_Id = 269, Bill_Name = "Table 6", Adult_Count = 2, Total_Count = 2 },
            new Billing { Bill_Id = 140, Session_Id = 143, Bill_Name = "Table 19 - Party of 4", Adult_Count = 4, Total_Count = 4 });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private BillController ControllerFor(int userId) =>
        new(_context, new Mock<IConfiguration>().Object, new Mock<ILogger<BillController>>().Object, new Mock<IPricingService>().Object)
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

    [Fact]
    public async Task Customers_CanSeeTheirOwnTablesBills()
    {
        var result = await ControllerFor(SeatedAtTable6).Get_Bills(269);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Customers_CantSeeAnotherTablesBills()
    {
        var controller = ControllerFor(SeatedAtTable6);

        (await controller.Get_Bills(143)).Should().BeOfType<ForbidResult>();
        (await controller.Get_Bill(143, 140)).Should().BeOfType<ForbidResult>();
        (await controller.GetBillSummary(143, 140)).Should().BeOfType<ForbidResult>();
    }
}
