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
using back_end.domain.enums;
using back_end.DTO.bill;
using back_end.Services;

namespace back_end.Tests.Controllers;

// Bill totals: 5% GST (Alberta), and an 18% gratuity once the table has 6 or more guests
public class BillGratuityTests : IDisposable
{
    private const int SessionId = 700;
    private const int BillId = 800;
    private const int StaffUser = 30;

    private readonly ApplicationDbContext _context;

    public BillGratuityTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _context.Menus.Add(new Menu { Menu_id = 1, Name = "Dinner", Description = "Dinner", Is_active = true });
        _context.Tables.Add(new TableEntity { Table_Id = 6, table_number = 6, Location_Id = 1, is_active = true });
        _context.DiningSessions.Add(new DiningSession { Session_Id = SessionId, Menu_Id = 1, Location_Id = 1, Table_Id = 6, Started_At = DateTime.UtcNow });
        // Staff only see sessions at the location they're working at
        _context.Users.Add(new User { User_id = StaffUser, Email = "staff@example.com", Role = UserRoles.Staff, Location_id = 1 });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private void AddBill(int billId, int adults, int children = 0, BillStatus status = BillStatus.Open)
    {
        _context.Bills.Add(new Billing
        {
            Bill_Id = billId,
            Session_Id = SessionId,
            Bill_Name = $"Bill {billId}",
            Adult_Count = adults,
            Child_Count = children,
            Total_Count = adults + children,
            Status = status,
        });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    private static ControllerContext AsStaff() => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, StaffUser.ToString()),
                new Claim(ClaimTypes.Role, "Staff"),
            }, "TestAuth")),
        },
    };

    // Adults $40, children $20; 5% GST; 18% gratuity from 6 guests
    private async Task<BillSummaryResponse> Summary(int billId = BillId)
    {
        var pricing = new Mock<IPricingService>();
        pricing.Setup(p => p.GetPricingAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new PricingInfo
               {
                   PricingType = "Test",
                   AdultBasePrice = 40m,
                   ChildBasePrice = 20m,
                   TaxRate = 0.05m,
                   GratuityRate = 0.18m,
                   GratuityMinPartySize = 6,
               });
        var controller = new BillController(_context, new Mock<IConfiguration>().Object, new Mock<ILogger<BillController>>().Object, pricing.Object)
        { ControllerContext = AsStaff() };

        var result = await controller.GetBillSummary(SessionId, billId);
        return ((result as OkObjectResult)!.Value as BillSummaryResponse)!;
    }

    [Fact]
    public async Task SmallParty_PaysGstOnly()
    {
        AddBill(BillId, adults: 4, children: 1);

        var summary = await Summary();

        summary.Subtotal.Should().Be(180m);
        summary.Party_Size.Should().Be(5);
        summary.Gratuity_Rate.Should().Be(0m);
        summary.Gratuity_Amount.Should().Be(0m);
        summary.Tax_Rate.Should().Be(0.05m);
        summary.Tax_Amount.Should().Be(9m);
        summary.Total_Amount.Should().Be(189m);
    }

    [Fact]
    public async Task PartyOfSix_AddsGratuity_AndGstOnTop()
    {
        AddBill(BillId, adults: 4, children: 2);

        var summary = await Summary();

        summary.Subtotal.Should().Be(200m);
        summary.Party_Size.Should().Be(6);
        summary.Gratuity_Rate.Should().Be(0.18m);
        summary.Gratuity_Amount.Should().Be(36m);
        summary.Gratuity_Min_Party_Size.Should().Be(6);
        // A mandatory gratuity is taxable: 5% of $236
        summary.Tax_Amount.Should().Be(11.80m);
        summary.Total_Amount.Should().Be(247.80m);
    }

    [Fact]
    public async Task BigTableSplitIntoSmallBills_StillGetsGratuity()
    {
        AddBill(BillId, adults: 3);
        AddBill(BillId + 1, adults: 3);

        var summary = await Summary();

        summary.Party_Size.Should().Be(6);
        summary.Gratuity_Amount.Should().Be(21.60m);   // 18% of $120
    }

    [Fact]
    public async Task CancelledBills_DontCountTowardsPartySize()
    {
        AddBill(BillId, adults: 3);
        AddBill(BillId + 1, adults: 3, status: BillStatus.Cancelled);

        var summary = await Summary();

        summary.Party_Size.Should().Be(3);
        summary.Gratuity_Amount.Should().Be(0m);
    }
}
